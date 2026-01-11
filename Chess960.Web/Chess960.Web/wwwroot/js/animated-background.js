// Make it global for easier Blazor interop
window.initAnimatedBackground = function (containerOrId) {
    console.log('[AnimatedBackground.js] Initializing with:', containerOrId);
    let container;
    if (typeof containerOrId === 'string') {
        container = document.getElementById(containerOrId);
    } else {
        container = containerOrId;
    }

    if (!container) {
        console.error('[AnimatedBackground.js] Container not found for:', containerOrId);
        return;
    }

    // Clear existing pieces if re-initializing
    container.innerHTML = '';

    const pieces = ['P', 'N', 'B', 'R', 'Q', 'K'];
    const count = 15;
    const elements = [];

    // Create pieces
    for (let i = 0; i < count; i++) {
        const pieceType = pieces[i % pieces.length];
        const el = document.createElement('img');
        el.src = `/images/pieces/merida/w${pieceType}.svg`;
        el.style.position = 'absolute';
        el.style.opacity = '0.2'; // Ensures visibility
        el.style.pointerEvents = 'none';

        // Random initial state
        const state = {
            x: Math.random() * 100, // %
            y: 110 + Math.random() * 50, // % (start below screen)
            speed: 0.05 + Math.random() * 0.05, // % per frame
            rotation: Math.random() * 360,
            rotationSpeed: (Math.random() - 0.5) * 0.5,
            size: 40 + Math.random() * 60, // px
            el: el
        };

        el.style.width = `${state.size}px`;
        el.style.height = `${state.size}px`;
        el.style.left = `${state.x}%`;

        container.appendChild(el);
        elements.push(state);
    }

    let animationId;

    function animate() {
        for (const item of elements) {
            // Update position
            item.y -= item.speed;
            item.rotation += item.rotationSpeed;

            // Reset if moved off top
            if (item.y < -20) {
                item.y = 110;
                item.x = Math.random() * 100;
            }

            // Apply styles
            item.el.style.transform = `translateY(${window.innerHeight * (item.y / 100) - window.innerHeight}px) rotate(${item.rotation}deg)`;
            item.el.style.top = `${item.y}%`; // Fallback/Basis
        }
        animationId = requestAnimationFrame(animate);
    }

    animate();

    return {
        dispose: () => {
            cancelAnimationFrame(animationId);
            container.innerHTML = '';
        }
    };
};

// Auto-initialize if the global container exists
(function () {
    const containerId = 'animated-bg-container';
    const init = () => {
        if (document.getElementById(containerId)) {
            console.log('[AnimatedBackground.js] Auto-initializing...');
            window.initAnimatedBackground(containerId);
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
