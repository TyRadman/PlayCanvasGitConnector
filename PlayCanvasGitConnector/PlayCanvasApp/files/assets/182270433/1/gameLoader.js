var GameLoader = pc.createScript('gameLoader');

GameLoader.prototype.initialize = function() {
        window.parent.postMessage({gameState:'gameLoaded'}, '*');
};