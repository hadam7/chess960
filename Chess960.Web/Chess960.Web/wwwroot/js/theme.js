window.themeService = {
    currentTheme: 'green',

    setTheme: function (themeName) {
        this.currentTheme = themeName;
        localStorage.setItem('chess960-theme', themeName);
        this.applyTheme();
    },

    getTheme: function () {
        return localStorage.getItem('chess960-theme') || 'green';
    },

    applyTheme: function () {
        if (document.documentElement.getAttribute('data-theme') !== this.currentTheme) {
            document.documentElement.setAttribute('data-theme', this.currentTheme);
        }
    },

    initTheme: function () {
        this.currentTheme = this.getTheme();
        this.applyTheme();

        // Use MutationObserver to enforce theme if Blazor/DOM resets it
        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                if (mutation.type === 'attributes' && mutation.attributeName === 'data-theme') {
                    const newVal = document.documentElement.getAttribute('data-theme');
                    if (newVal !== this.currentTheme) {
                        // console.log("Theme reverted detected, reapplying:", this.currentTheme);
                        this.applyTheme();
                    }
                }
            });
        });

        observer.observe(document.documentElement, { attributes: true });
    }
};

// Initialize on load
window.themeService.initTheme();
