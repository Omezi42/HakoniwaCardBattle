mergeInto(LibraryManager.library, {
  
  // -- Clipboard --
  
  CopyToClipboardJS: function (str) {
    var text = UTF8ToString(str);
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(function() {
        console.log("Clipboard Copy Success");
      }, function(err) {
        console.error("Clipboard Copy Failed: ", err);
      });
    } else {
        // Fallback
        var textArea = document.createElement("textarea");
        textArea.value = text;
        document.body.appendChild(textArea);
        textArea.focus();
        textArea.select();
        try {
            document.execCommand('copy');
        } catch (err) {
            console.error('Fallback Copy failed', err);
        }
        document.body.removeChild(textArea);
    }
  },

  RequestPasteFromClipboardJS: function (gameObjectName, methodName) {
    var goName = UTF8ToString(gameObjectName);
    var funcName = UTF8ToString(methodName);
    
    if (navigator.clipboard && navigator.clipboard.readText) {
      navigator.clipboard.readText().then(function(text) {
        // Send back to Unity
        SendMessage(goName, funcName, text);
      }).catch(function(err) {
        console.error("Clipboard Read Failed: ", err);
      });
    } else {
      console.warn("Clipboard Read not supported/allowed");
    }
  },

});
