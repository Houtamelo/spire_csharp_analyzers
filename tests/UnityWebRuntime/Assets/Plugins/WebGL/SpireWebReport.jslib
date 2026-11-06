mergeInto(LibraryManager.library, {
  SpireWebReport: function (kind, json) {
    globalThis.__spireWebResults = globalThis.__spireWebResults || {};
    globalThis.__spireWebResults[UTF8ToString(kind)] = JSON.parse(UTF8ToString(json));
  }
});
