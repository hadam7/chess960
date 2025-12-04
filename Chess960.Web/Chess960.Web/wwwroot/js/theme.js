window.themeService = {
    setTheme: function (themeName) {
        document.documentElement.setAttribute('data-theme', themeName);
        localStorage.setItem('chess960-theme', themeName);
    },
    getTheme: function () {
        return localStorage.getItem('chess960-theme') || 'green';
    },
    initTheme: function () {
        const theme = this.getTheme();
        this.setTheme(theme);
    }
};

// Initialize on load
window.themeService.initTheme();
