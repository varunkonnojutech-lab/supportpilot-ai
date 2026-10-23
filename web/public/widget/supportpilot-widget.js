/**
 * SupportPilot AI — embeddable chat widget.
 *
 * Usage (one script tag, pasted before </body> on any page):
 *
 *   <script src="https://your-supportpilot-host/widget/supportpilot-widget.js"
 *           data-widget-key="sp_..." async></script>
 *
 * The widget:
 *   - reads its tenant key from data-widget-key on its own <script> tag,
 *   - talks to the API on the SAME origin that served this script,
 *   - renders inside a Shadow DOM so host-page CSS can never clash with it
 *     (and widget CSS can never leak into the host page),
 *   - builds every message with textContent — user/assistant text is never
 *     injected as HTML.
 *
 * Public API used (no auth):
 *   GET  {origin}/api/public/widget/{widgetKey}
 *        -> { businessName, color, greeting }
 *   POST {origin}/api/public/chat
 *        { widgetKey, sessionId?, message } -> { sessionId, assistantMessageId,
 *        answer, citations: [{ documentTitle, snippet }] }
 *   POST {origin}/api/public/chat/feedback
 *        { sessionId, messageId, rating }   (rating: "up" | "down")
 */
(function () {
  "use strict";

  // ---------------------------------------------------------------- setup --
  var script =
    document.currentScript ||
    document.querySelector("script[data-widget-key][src*='supportpilot-widget']");

  if (!script) return;
  if (window.__supportPilotWidgetLoaded) return;
  window.__supportPilotWidgetLoaded = true;

  var widgetKey = script.getAttribute("data-widget-key") || "";
  if (!widgetKey) return;

  var apiBase;
  try {
    apiBase = new URL(script.src, window.location.href).origin;
  } catch (e) {
    apiBase = window.location.origin;
  }

  var DEFAULT_COLOR = "#4f46e5";
  var config = {
    businessName: "Support",
    color: DEFAULT_COLOR,
    greeting: "Hi! How can we help you today?",
  };

  var sessionId = null;
  try {
    sessionId = window.sessionStorage.getItem("sp_widget_session_" + widgetKey);
  } catch (e) {
    sessionId = null;
  }

  var isOpen = false;
  var sending = false;
  var greetingShown = false;

  // ------------------------------------------------------------- helpers ---
  function el(tag, className, text) {
    var node = document.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined && text !== null) node.textContent = text;
    return node;
  }

  function storeSession(id) {
    sessionId = id;
    try {
      if (id) {
        window.sessionStorage.setItem("sp_widget_session_" + widgetKey, id);
      }
    } catch (e) {
      /* sessionStorage unavailable (private mode etc.) — session stays in memory */
    }
  }

  function apiPost(path, body) {
    return fetch(apiBase + path, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    }).then(function (res) {
      return res
        .json()
        .catch(function () {
          return null;
        })
        .then(function (data) {
          if (!res.ok) {
            var message =
              data && typeof data.error === "string"
                ? data.error
                : "Something went wrong (HTTP " + res.status + ").";
            throw new Error(message);
          }
          return data;
        });
    });
  }

  // ------------------------------------------------------------ shadow DOM -
  var host = el("div");
  host.id = "supportpilot-widget-host";
  var shadow = host.attachShadow({ mode: "open" });

  var style = el("style");
  style.textContent = [
    ":host { all: initial; }",
    "* { box-sizing: border-box; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Arial, sans-serif; }",
    ".sp-bubble { position: fixed; right: 20px; bottom: 20px; z-index: 2147483000; width: 60px; height: 60px; border: none; border-radius: 50%; color: #fff; font-size: 26px; line-height: 1; cursor: pointer; box-shadow: 0 8px 24px rgba(0,0,0,.25); display: flex; align-items: center; justify-content: center; }",
    ".sp-bubble:hover { filter: brightness(1.08); }",
    ".sp-panel { position: fixed; right: 20px; bottom: 92px; z-index: 2147483000; width: 370px; max-width: calc(100vw - 32px); height: 540px; max-height: calc(100vh - 120px); background: #fff; border-radius: 16px; box-shadow: 0 16px 48px rgba(0,0,0,.28); display: none; flex-direction: column; overflow: hidden; }",
    ".sp-panel.sp-open { display: flex; }",
    ".sp-header { color: #fff; padding: 14px 16px; display: flex; align-items: center; gap: 10px; }",
    ".sp-header-name { font-weight: 700; font-size: 15px; flex: 1; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }",
    ".sp-header-sub { font-size: 12px; opacity: .85; }",
    ".sp-close { background: transparent; border: none; color: #fff; font-size: 20px; cursor: pointer; padding: 2px 6px; line-height: 1; }",
    ".sp-messages { flex: 1; overflow-y: auto; padding: 14px; background: #f9fafb; display: flex; flex-direction: column; gap: 10px; }",
    ".sp-msg { max-width: 85%; padding: 9px 12px; border-radius: 14px; font-size: 14px; line-height: 1.45; white-space: pre-wrap; word-break: break-word; }",
    ".sp-msg-user { align-self: flex-end; color: #fff; border-bottom-right-radius: 4px; }",
    ".sp-msg-assistant { align-self: flex-start; background: #fff; border: 1px solid #e5e7eb; color: #111827; border-bottom-left-radius: 4px; }",
    ".sp-msg-error { align-self: flex-start; background: #fef2f2; border: 1px solid #fecaca; color: #991b1b; }",
    ".sp-sources { margin-top: 7px; padding-top: 6px; border-top: 1px solid #e5e7eb; font-size: 12px; color: #6b7280; white-space: normal; }",
    ".sp-sources strong { color: #4b5563; }",
    ".sp-feedback { margin-top: 6px; display: flex; gap: 6px; }",
    ".sp-feedback button { background: #f3f4f6; border: 1px solid #e5e7eb; border-radius: 999px; padding: 2px 9px; font-size: 13px; cursor: pointer; }",
    ".sp-feedback button:hover { background: #e5e7eb; }",
    ".sp-feedback button.sp-voted { background: #eef2ff; border-color: #c7d2fe; }",
    ".sp-feedback span { font-size: 12px; color: #6b7280; align-self: center; }",
    ".sp-typing { align-self: flex-start; color: #6b7280; font-size: 13px; padding: 4px 2px; }",
    ".sp-form { display: flex; gap: 8px; padding: 10px; border-top: 1px solid #e5e7eb; background: #fff; }",
    ".sp-input { flex: 1; border: 1px solid #d1d5db; border-radius: 10px; padding: 9px 11px; font-size: 14px; color: #111827; background: #fff; }",
    ".sp-input:focus { outline: 2px solid #eef2ff; border-color: #9ca3af; }",
    ".sp-send { border: none; border-radius: 10px; color: #fff; font-size: 14px; font-weight: 600; padding: 9px 16px; cursor: pointer; }",
    ".sp-send:disabled { opacity: .55; cursor: not-allowed; }",
    ".sp-powered { text-align: center; font-size: 11px; color: #9ca3af; padding: 0 0 8px; background: #fff; }",
  ].join("\n");
  shadow.appendChild(style);

  // Bubble ------------------------------------------------------------
  var bubble = el("button", "sp-bubble", "💬");
  bubble.type = "button";
  bubble.setAttribute("aria-label", "Chat with us");
  bubble.style.background = config.color;

  // Panel -------------------------------------------------------------
  var panel = el("div", "sp-panel");
  panel.setAttribute("role", "dialog");
  panel.setAttribute("aria-label", "Support chat");

  var header = el("div", "sp-header");
  header.style.background = config.color;
  var headerText = el("div");
  var headerName = el("div", "sp-header-name", config.businessName);
  var headerSub = el("div", "sp-header-sub", "Typically replies instantly");
  headerText.appendChild(headerName);
  headerText.appendChild(headerSub);
  var closeBtn = el("button", "sp-close", "×");
  closeBtn.type = "button";
  closeBtn.setAttribute("aria-label", "Close chat");
  header.appendChild(headerText);
  header.appendChild(closeBtn);

  var messages = el("div", "sp-messages");

  var form = el("form", "sp-form");
  var input = el("input", "sp-input");
  input.type = "text";
  input.placeholder = "Type your question…";
  input.setAttribute("aria-label", "Type your question");
  input.maxLength = 4000;
  var sendBtn = el("button", "sp-send", "Send");
  sendBtn.type = "submit";
  sendBtn.style.background = config.color;
  form.appendChild(input);
  form.appendChild(sendBtn);

  var powered = el("div", "sp-powered", "Powered by SupportPilot AI");

  panel.appendChild(header);
  panel.appendChild(messages);
  panel.appendChild(form);
  panel.appendChild(powered);

  shadow.appendChild(bubble);
  shadow.appendChild(panel);

  function mount() {
    if (document.body) {
      document.body.appendChild(host);
    } else {
      document.addEventListener("DOMContentLoaded", function () {
        document.body.appendChild(host);
      });
    }
  }
  mount();

  function applyConfig() {
    bubble.style.background = config.color;
    header.style.background = config.color;
    sendBtn.style.background = config.color;
    headerName.textContent = config.businessName;
    panel.setAttribute("aria-label", "Chat with " + config.businessName);
  }

  // ------------------------------------------------------------- messages --
  function scrollToBottom() {
    messages.scrollTop = messages.scrollHeight;
  }

  function addMessage(text, kind) {
    var node = el("div", "sp-msg sp-msg-" + kind, text);
    if (kind === "user") node.style.background = config.color;
    messages.appendChild(node);
    scrollToBottom();
    return node;
  }

  function addAssistantMessage(text, citations, messageId) {
    var node = addMessage(text, "assistant");

    if (citations && citations.length > 0) {
      var sources = el("div", "sp-sources");
      var titles = [];
      citations.forEach(function (citation) {
        if (citation && citation.documentTitle && titles.indexOf(citation.documentTitle) === -1) {
          titles.push(citation.documentTitle);
        }
      });
      if (titles.length > 0) {
        var label = el("strong", null, "Sources: ");
        sources.appendChild(label);
        sources.appendChild(document.createTextNode(titles.join(", ")));
        node.appendChild(sources);
      }
    }

    if (messageId) {
      var feedback = el("div", "sp-feedback");
      var up = el("button", null, "👍");
      up.type = "button";
      up.setAttribute("aria-label", "Helpful");
      var down = el("button", null, "👎");
      down.type = "button";
      down.setAttribute("aria-label", "Not helpful");
      var thanks = el("span", null, "");
      feedback.appendChild(up);
      feedback.appendChild(down);
      feedback.appendChild(thanks);
      node.appendChild(feedback);

      function vote(rating, button) {
        if (!sessionId) return;
        up.disabled = true;
        down.disabled = true;
        button.classList.add("sp-voted");
        thanks.textContent = "Thanks for the feedback!";
        apiPost("/api/public/chat/feedback", {
          sessionId: sessionId,
          messageId: messageId,
          rating: rating,
        }).catch(function () {
          thanks.textContent = "";
          up.disabled = false;
          down.disabled = false;
          button.classList.remove("sp-voted");
        });
      }

      up.addEventListener("click", function () {
        vote("up", up);
      });
      down.addEventListener("click", function () {
        vote("down", down);
      });
    }

    scrollToBottom();
    return node;
  }

  function showGreeting() {
    if (greetingShown) return;
    greetingShown = true;
    if (config.greeting) addAssistantMessage(config.greeting, [], null);
  }

  // ---------------------------------------------------------------- chat ---
  function sendMessage(text) {
    if (sending || !text) return;
    sending = true;
    sendBtn.disabled = true;
    input.disabled = true;

    addMessage(text, "user");
    var typing = el("div", "sp-typing", config.businessName + " is typing…");
    messages.appendChild(typing);
    scrollToBottom();

    var body = { widgetKey: widgetKey, message: text };
    if (sessionId) body.sessionId = sessionId;

    apiPost("/api/public/chat", body)
      .then(function (data) {
        typing.remove();
        if (data && data.sessionId) storeSession(data.sessionId);
        var answer =
          data && typeof data.answer === "string" && data.answer
            ? data.answer
            : "Sorry, I couldn't come up with an answer to that. Please try rephrasing your question.";
        addAssistantMessage(
          answer,
          data && Array.isArray(data.citations) ? data.citations : [],
          data ? data.assistantMessageId : null,
        );
      })
      .catch(function (err) {
        typing.remove();
        var message =
          err && err.message
            ? err.message
            : "We couldn't reach the support service. Please check your connection and try again.";
        addMessage(
          "⚠️ " + message + " If this keeps happening, please contact us directly.",
          "error",
        );
      })
      .then(function () {
        sending = false;
        sendBtn.disabled = false;
        input.disabled = false;
        input.focus();
      });
  }

  form.addEventListener("submit", function (event) {
    event.preventDefault();
    var text = input.value.trim();
    if (!text) return;
    input.value = "";
    sendMessage(text);
  });

  // -------------------------------------------------------------- open/close
  function setOpen(open) {
    isOpen = open;
    panel.classList.toggle("sp-open", open);
    bubble.textContent = open ? "✕" : "💬";
    bubble.setAttribute("aria-label", open ? "Close chat" : "Chat with us");
    if (open) {
      showGreeting();
      input.focus();
    }
  }

  bubble.addEventListener("click", function () {
    setOpen(!isOpen);
  });
  closeBtn.addEventListener("click", function () {
    setOpen(false);
    bubble.focus();
  });

  // ------------------------------------------------------------ load config
  fetch(apiBase + "/api/public/widget/" + encodeURIComponent(widgetKey), {
    headers: { Accept: "application/json" },
  })
    .then(function (res) {
      if (!res.ok) throw new Error("Widget config failed: HTTP " + res.status);
      return res.json();
    })
    .then(function (data) {
      if (!data) return;
      if (typeof data.businessName === "string" && data.businessName) {
        config.businessName = data.businessName;
      }
      if (typeof data.color === "string" && /^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/.test(data.color)) {
        config.color = data.color;
      }
      if (typeof data.greeting === "string" && data.greeting) {
        config.greeting = data.greeting;
      }
      applyConfig();
    })
    .catch(function () {
      // The widget still renders with defaults; opening it explains the
      // problem instead of failing silently.
      applyConfig();
      if (!greetingShown) {
        greetingShown = true;
        addAssistantMessage(
          "We're having trouble connecting to our support service right now. Please try again in a little while, or contact us directly.",
          [],
          null,
        );
      }
    });
})();
