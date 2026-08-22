// Injected before any page script. Implements the window.glass contract in
// terms of WebView2 postMessage, so the existing renderer runs unchanged on the
// native host. The host substitutes the initial-settings placeholder below with
// the real (compact, single-line) settings JSON so window.glass.initialSettings
// is correct on the very first render.
(function () {
  var initial = __GLASS_INITIAL_JSON__;
  var wv = window.chrome && window.chrome.webview;
  var seq = 0;
  var pending = {};
  var listeners = [];

  function send(type, payload) {
    return new Promise(function (resolve) {
      if (!wv) { resolve(initial); return; }
      var id = ++seq;
      pending[id] = resolve;
      wv.postMessage(JSON.stringify({ id: id, type: type, payload: payload || null }));
    });
  }

  if (wv) {
    wv.addEventListener('message', function (e) {
      var msg;
      try { msg = JSON.parse(e.data); } catch (err) { return; }
      if (msg && msg.type === 'settingsChanged') {
        listeners.slice().forEach(function (fn) { try { fn(msg.payload); } catch (err) {} });
        return;
      }
      if (msg && msg.id && Object.prototype.hasOwnProperty.call(pending, msg.id)) {
        var resolve = pending[msg.id];
        delete pending[msg.id];
        resolve(msg.result);
      }
    });
  }

  window.glass = {
    initialSettings: initial,
    getSettings: function () { return send('getSettings'); },
    saveSettings: function (patch) { return send('saveSettings', patch); },
    selectBackgroundImage: function () { return send('selectBackgroundImage'); },
    quit: function () { if (wv) { wv.postMessage(JSON.stringify({ type: 'quit' })); } },
    onSettingsChanged: function (cb) {
      listeners.push(cb);
      return function () {
        var i = listeners.indexOf(cb);
        if (i >= 0) { listeners.splice(i, 1); }
      };
    }
  };
})();
