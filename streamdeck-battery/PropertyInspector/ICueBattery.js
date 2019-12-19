function openWebsiteICue() {
    if (websocket && (websocket.readyState === 1)) {
        const json = {
            'event': 'openUrl',
            'payload': {
                'url': 'https://BarRaider.github.io/iCue'
            }
        };
        websocket.send(JSON.stringify(json));
    }
}
