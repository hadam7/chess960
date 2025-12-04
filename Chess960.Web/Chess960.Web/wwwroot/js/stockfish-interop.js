window.stockfishInterop = {
    worker: null,
    blazorInstance: null,

    init: function (dotNetHelper) {
        if (this.worker) {
            this.worker.terminate();
        }

        this.blazorInstance = dotNetHelper;
        // Path relative to wwwroot
        this.worker = new Worker('/lib/stockfish/stockfish.js');

        this.worker.onmessage = (event) => {
            const message = event.data;
            // console.log("Stockfish says: " + message);

            if (message.startsWith('bestmove')) {
                const parts = message.split(' ');
                const move = parts[1];
                // Check if there is a ponder move, but we only care about bestmove
                this.blazorInstance.invokeMethodAsync('OnBestMove', move);
            }
        };

        this.worker.postMessage('uci');
    },

    startNewGame: function (skillLevel) {
        if (!this.worker) return;
        this.worker.postMessage('ucinewgame');
        this.worker.postMessage('isready');
        this.worker.postMessage(`setoption name Skill Level value ${skillLevel}`);
    },

    requestMove: function (fen, depth) {
        if (!this.worker) return;
        this.worker.postMessage(`position fen ${fen}`);
        this.worker.postMessage(`go depth ${depth}`);
    }
};
