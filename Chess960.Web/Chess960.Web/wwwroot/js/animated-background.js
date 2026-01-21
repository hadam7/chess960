// Make it global for easier Blazor interop
window.initAnimatedBackground = function (containerOrId) {
    let container;
    if (typeof containerOrId === 'string') {
        container = document.getElementById(containerOrId);
    } else {
        container = containerOrId;
    }

    if (!container) return;

    // Clear existing
    container.innerHTML = '';
    container.style.position = 'absolute';
    container.style.inset = '0';
    container.style.overflow = 'hidden';
    container.style.pointerEvents = 'none'; // Click-through

    const pieces = ['P', 'N', 'B', 'R', 'Q', 'K'];
    const count = 15;
    const elements = [];

    // Create pieces
    for (let i = 0; i < count; i++) {
        const pieceType = pieces[i % pieces.length];
        const el = document.createElement('img');
        el.src = `/images/pieces/merida/w${pieceType}.svg`;
        el.style.position = 'absolute';

        // Subtle background pieces (No Glow)
        el.style.opacity = '0.08';
        el.style.filter = 'grayscale(100%)'; // Ensure they are neutral
        el.style.pointerEvents = 'none';

        // Random initial state
        const state = {
            x: Math.random() * 100, // %
            y: -20 - Math.random() * 50, // Start ABOVE viewport
            speed: 0.02 + Math.random() * 0.04, // Falling speed
            rotation: Math.random() * 360,
            rotationSpeed: (Math.random() - 0.5) * 0.5,
            size: 30 + Math.random() * 60, // px
            el: el
        };

        el.style.width = `${state.size}px`;
        el.style.height = `${state.size}px`;
        el.style.left = `${state.x}%`;

        // Initial transform
        const yPx = window.innerHeight * (state.y / 100);
        el.style.transform = `translate3d(0, ${yPx}px, 0)`;

        container.appendChild(el);
        elements.push(state);
    }

    let animationId;
    let lastTime = 0;

    function animate(timestamp) {
        if (!lastTime) lastTime = timestamp;

        for (const item of elements) {
            // Update position (FALLING DOWN)
            item.y += item.speed;
            item.rotation += item.rotationSpeed;

            // Reset when it goes below screen
            if (item.y > 110) {
                item.y = -20;
                item.x = Math.random() * 100;
            }

            // Apply transform (GPU accelerated)
            // y is percentage of screen height? 
            // Previous logic: window.innerHeight * (item.y / 100) - window.innerHeight; was weird.
            // Let's just map y% to pixels directly. 0% = top, 100% = bottom.
            const yPx = window.innerHeight * (item.y / 100);

            item.el.style.transform = `translate3d(0, ${yPx}px, 0) rotate(${item.rotation}deg)`;
            item.el.style.left = `${item.x}%`;
        }

        lastTime = timestamp;
        animationId = requestAnimationFrame(animate);
    }

    animationId = requestAnimationFrame(animate);

    return {
        dispose: () => {
            cancelAnimationFrame(animationId);
            container.innerHTML = '';
        }
    };
};

(function () {
    const containerId = 'animated-bg-container';
    const init = () => {
        if (document.getElementById(containerId)) {
            window.initAnimatedBackground(containerId);
        }
    };
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
