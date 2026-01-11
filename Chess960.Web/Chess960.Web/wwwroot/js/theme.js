window.themeService = {
    currentTheme: 'green',
    currentBoardTheme: 'default',
    currentPieceTheme: 'standard',

    setTheme: function (themeName) {
        this.currentTheme = themeName;
        localStorage.setItem('chess960-theme', themeName);
        this.applyTheme();
    },

    setBoardTheme: function (themeName) {
        this.currentBoardTheme = themeName;
        localStorage.setItem('chess960-board-theme', themeName);
        this.applyTheme();
    },

    setPieceTheme: function (themeName) {
        this.currentPieceTheme = themeName;
        localStorage.setItem('chess960-piece-theme', themeName);
    },

    getTheme: function () {
        return localStorage.getItem('chess960-theme') || 'green';
    },

    getBoardTheme: function () {
        return localStorage.getItem('chess960-board-theme') || 'default';
    },

    getPieceTheme: function () {
        return localStorage.getItem('chess960-piece-theme') || 'standard';
    },

    applyTheme: function () {
        if (document.documentElement.getAttribute('data-theme') !== this.currentTheme) {
            document.documentElement.setAttribute('data-theme', this.currentTheme);
        }
        if (document.documentElement.getAttribute('data-board-theme') !== this.currentBoardTheme) {
            document.documentElement.setAttribute('data-board-theme', this.currentBoardTheme);
        }
    },

    initTheme: function () {
        this.currentTheme = this.getTheme();
        this.currentBoardTheme = this.getBoardTheme();
        this.currentPieceTheme = this.getPieceTheme();
        this.applyTheme();

        // Use MutationObserver to enforce theme if Blazor/DOM resets it
        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                if (mutation.type === 'attributes') {
                    if (mutation.attributeName === 'data-theme') {
                        const newVal = document.documentElement.getAttribute('data-theme');
                        if (newVal !== this.currentTheme) {
                            this.applyTheme();
                        }
                    }
                    if (mutation.attributeName === 'data-board-theme') {
                        const newVal = document.documentElement.getAttribute('data-board-theme');
                        if (newVal !== this.currentBoardTheme) {
                            this.applyTheme();
                        }
                    }
                }
            });
        });

        observer.observe(document.documentElement, { attributes: true });
    }
};

// Initialize on load
window.themeService.initTheme();
