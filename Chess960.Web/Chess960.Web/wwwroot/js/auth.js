function togglePanel() {
    const container = document.getElementById('container');
    if (container) {
        container.classList.toggle('right-panel-active');
    } else {
        console.warn('Auth container not found');
    }
}
