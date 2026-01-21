
// Game Analysis & Stockfish Integration
let stockfish = null;
let currentFen = "";
let boardOverlay = null;

export function initAnalysis() {
    console.log("[Analysis] Initializing...");

    const container = document.getElementById('replay-board-container');
    const evalDisplay = document.getElementById('eval-display');
    boardOverlay = document.getElementById('analysis-overlay');

    if (!container || !evalDisplay) {
        console.warn("[Analysis] Container or Display not found.");
        return;
    }

    // Initialize Stockfish
    if (!stockfish) {
        console.log("[Analysis] Creating Stockfish Worker...");
        try {
            stockfish = new Worker('/lib/stockfish/stockfish.js');
            stockfish.onerror = (err) => console.error("[Analysis] Worker Error:", err);

            stockfish.postMessage('uci');
            stockfish.postMessage('setoption name Hash value 32');
            stockfish.postMessage('isready');

            stockfish.onmessage = (e) => {
                const msg = e.data;
                // Parse Info
                if (msg.startsWith('info depth') && msg.includes('score')) {
                    parseInfo(msg);
                }
                if (msg === 'readyok') {
                    console.log("[Analysis] Engine Ready!");
                    // Re-trigger analysis once ready if we have a FEN
                    const current = container.getAttribute('data-fen');
                    if (current) analyze(current);
                }
            };
        } catch (e) {
            console.error("[Analysis] Failed to create Worker:", e);
        }
    }

    // Initial analysis
    const initialFen = container.getAttribute('data-fen');
    console.log("[Analysis] Initial FEN:", initialFen);
    if (initialFen) analyze(initialFen);

    // Observe changes to 'data-fen' (re-init observer if needed)
    if (window._analysisObserver) window._analysisObserver.disconnect();

    window._analysisObserver = new MutationObserver((mutations) => {
        mutations.forEach((mutation) => {
            if (mutation.type === 'attributes' && mutation.attributeName === 'data-fen') {
                const newFen = container.getAttribute('data-fen');
                console.log("[Analysis] FEN changed to:", newFen);
                if (newFen && newFen !== currentFen) {
                    analyze(newFen);
                }
            }
        });
    });

    window._analysisObserver.observe(container, { attributes: true });
}

function analyze(fen) {
    currentFen = fen;
    // console.log("[Analysis] Analyzing FEN:", fen);
    clearArrow();

    // Update status
    const display = document.getElementById('eval-display');
    if (display) display.innerHTML = '<span class="animate-pulse">⏳</span>';

    // Stop previous
    stockfish.postMessage('stop');
    stockfish.postMessage(`position fen ${fen}`);
    stockfish.postMessage('go depth 20'); // Lower depth for faster response
}

function parseInfo(msg) {
    // Example: info depth 10 ... score cp 50 ... pv e2e4 c7c5

    // Parse Side to Move from FEN
    // Standard FEN: rnbqk... w KQkq - 0 1
    const parts = currentFen.split(' ');
    // Default to 'w' if something weird happens, but standard FEN always has it.
    const activeColor = parts.length >= 2 ? parts[1] : 'w';
    const isWhiteTurn = (activeColor === 'w');

    // Extract Score (from Engine's Perspective)
    let engineScore = 0;
    let isMate = false;

    if (msg.includes('score mate')) {
        const mate = msg.match(/score mate (-?\d+)/);
        if (mate) {
            engineScore = parseInt(mate[1]);
            isMate = true;
        }
    } else if (msg.includes('score cp')) {
        const cp = msg.match(/score cp (-?\d+)/);
        if (cp) {
            engineScore = parseInt(cp[1]) / 100.0;
        }
    }

    // Convert to Absolute Score (White Perspective)
    // If Black to move (active='b'), and Engine says +1.0 (Black winning), Absolute is -1.0.
    // If Black to move (active='b'), and Engine says -1.0 (White winning), Absolute is +1.0.
    // If White to move (active='w'), Engine Score is already Absolute.

    let absoluteScore = isWhiteTurn ? engineScore : (engineScore * -1);

    let scoreText = "";

    if (isMate) {
        let moves = Math.abs(engineScore);
        if (absoluteScore > 0) scoreText = `M${moves}`; // White matches
        else scoreText = `-M${moves}`; // Black mates
    } else {
        scoreText = absoluteScore > 0 ? `+${absoluteScore.toFixed(2)}` : absoluteScore.toFixed(2);
    }

    // Extract Best Move (PV)
    let bestMove = "";
    const pvMatch = msg.match(/ pv ([a-h1-8]{4,5})/);
    if (pvMatch) {
        bestMove = pvMatch[1];
    }

    // Update UI
    updateUI(scoreText, absoluteScore, bestMove);
}

function updateUI(scoreText, scoreVal, bestMove) {
    const display = document.getElementById('eval-display');
    if (display) {
        // Color based on score (White Perspective)
        let colorClass = "text-white";

        // Thresholds
        if (scoreVal > 0.5) colorClass = "text-green-400"; // White winning
        else if (scoreVal < -0.5) colorClass = "text-red-400"; // Black winning
        else colorClass = "text-gray-300"; // Drawish

        display.className = `font-mono font-bold text-lg ${colorClass} bg-black/80 px-3 py-1 rounded border border-white/20 shadow-lg backdrop-blur`;
        display.innerText = scoreText;
    }

    if (bestMove) {
        drawArrow(bestMove);
    }
}

function drawArrow(moveStr) {
    if (!boardOverlay) return;

    // moveStr e.g. "e2e4"
    if (!moveStr || moveStr.length < 4) return;

    const from = moveStr.substring(0, 2);
    const to = moveStr.substring(2, 4);

    const fromCoords = getSquareCenter(from);
    const toCoords = getSquareCenter(to);

    const svg = `
        <svg viewBox="0 0 100 100" class="w-full h-full absolute top-0 left-0 pointer-events-none overflow-visible">
            <defs>
                <marker id="arrowhead" markerWidth="4" markerHeight="4" refX="2" refY="2" orient="auto">
                    <path d="M0,0 L4,2 L0,4 Z" fill="rgba(2, 233, 206, 0.8)" />
                </marker>
            </defs>
            <line x1="${fromCoords.x}" y1="${fromCoords.y}" x2="${toCoords.x}" y2="${toCoords.y}" 
                  stroke="rgba(2, 233, 206, 0.8)" stroke-width="2" marker-end="url(#arrowhead)" opacity="0.8" />
        </svg>
    `;

    boardOverlay.innerHTML = svg;
}

function clearArrow() {
    if (boardOverlay) boardOverlay.innerHTML = "";
}

function getSquareCenter(square) {
    // "e2" -> file e (4), rank 2.
    // Files: a=0, h=7.
    // Ranks: 1=0, 8=7.

    const fileChar = square.charAt(0);
    const rankChar = square.charAt(1);

    const file = fileChar.charCodeAt(0) - 'a'.charCodeAt(0);
    const rank = parseInt(rankChar) - 1;

    // Board is White Side (Rank 0 is Bottom).
    // File 0 is Left.
    // CSS Grid: x% = (file * 12.5) + 6.25
    // y% = ((7 - rank) * 12.5) + 6.25

    return {
        x: (file * 12.5) + 6.25,
        y: ((7 - rank) * 12.5) + 6.25
    };
}

// Auto-start
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initAnalysis);
} else {
    initAnalysis();
}
