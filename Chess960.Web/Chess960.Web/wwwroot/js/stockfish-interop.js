let worker = null;
let blazorInstance = null;

export function init(dotNetHelper) {
    if (worker) {
        worker.terminate();
    }

    blazorInstance = dotNetHelper;
    // Path relative to wwwroot
    worker = new Worker('/lib/stockfish/stockfish.js');

    worker.onmessage = (event) => {
        const message = event.data;
        // console.log("Stockfish says: " + message);

        if (message.startsWith('bestmove')) {
            const parts = message.split(' ');
            const move = parts[1];
            // Check if there is a ponder move, but we only care about bestmove
            blazorInstance.invokeMethodAsync('OnBestMove', move);
        }
    };

    worker.postMessage('uci');
}

export function startNewGame(skillLevel) {
    if (!worker) return;
    worker.postMessage('ucinewgame');
    worker.postMessage('isready');
    worker.postMessage(`setoption name Skill Level value ${skillLevel}`);
}

export function requestMove(fen, depth) {
    if (!worker) return;
    worker.postMessage(`position fen ${fen}`);
    worker.postMessage(`go depth ${depth}`);
}
