export let audioCache = {};

export function initAudio() {
    const sounds = ['move', 'capture', 'notify', 'game-start', 'game-end'];

    sounds.forEach(sound => {
        const audio = new Audio(`/sounds/${sound}.mp3`);
        audio.preload = 'auto';
        audioCache[sound] = audio;
    });
}

export function playSound(soundName) {
    const audio = audioCache[soundName];
    if (audio) {
        audio.currentTime = 0;
        audio.play().catch(e => console.log("Audio play failed:", e));
    } else {
        // Fallback or lazy load
        const newAudio = new Audio(`/sounds/${soundName}.mp3`);
        newAudio.play().catch(e => console.log("Audio play failed:", e));
        audioCache[soundName] = newAudio;
    }
}
