const ordersBody = document.querySelector("#orders");
const ordersEmpty = document.querySelector("#orders-empty");
const sentList = document.querySelector("#sent");
const receivedList = document.querySelector("#received");
const orderNumberInput = document.querySelector("#order-number");
const notice = document.querySelector("#notice");

let selectedId = null;
let polling = false;

document.querySelector("#create-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const orderNumber = orderNumberInput.value.trim();
  if (!orderNumber) {
    return;
  }

  const response = await fetch("/orders", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ orderNumber }),
  });
  if (!response.ok) {
    setNotice(noticeFor(response.status, "create"));
    return;
  }

  const created = await response.json();
  selectedId = created.correlationId;
  orderNumberInput.value = "";
  setNotice("");
  await refresh();
});

ordersBody.addEventListener("click", async (event) => {
  const row = event.target.closest("tr[data-id]");
  if (!row) {
    return;
  }

  selectedId = row.dataset.id;
  const button = event.target.closest("[data-command]");
  if (button) {
    const response = await fetch(`/orders/${selectedId}/events`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name: button.dataset.command }),
    });
    if (!response.ok) {
      setNotice(noticeFor(response.status, "command"));
      await refresh(true);
      return;
    }
  }

  setNotice("");
  await refresh();
});

function setNotice(message) {
  const text = message ?? "";
  notice.textContent = text;
  notice.hidden = text.length === 0;
}

function noticeFor(status, kind) {
  if (status === 429) {
    return "This demo is busy. Try again in a second.";
  }

  if (status === 409 && kind === "create") {
    return "This demo already has as many orders as it keeps.";
  }

  if (status === 409) {
    return "That action is no longer available.";
  }

  if (status === 404) {
    return "That order is no longer listed.";
  }

  return "That request was not accepted.";
}

async function refresh(keepNotice = false) {
  if (polling) {
    return;
  }

  polling = true;
  try {
    // Query everything first; the page is only touched once all answers are in.
    const id = selectedId;
    const [ordersResponse, eventsResponse] = await Promise.all([
      fetch("/orders"),
      id ? fetch(`/orders/${id}/events`) : null,
    ]);
    if (!ordersResponse.ok) {
      if (ordersResponse.status === 429) {
        setNotice(noticeFor(429));
      }
      return;
    }

    if (eventsResponse && !eventsResponse.ok && eventsResponse.status === 429) {
      setNotice(noticeFor(429));
      keepNotice = true;
    }

    const orders = await ordersResponse.json();
    const events = eventsResponse?.ok ? await eventsResponse.json() : null;
    if (!keepNotice) {
      setNotice("");
    }

    // No await below: the old content is replaced in one step, so no frame shows empty lists.
    renderOrders(orders);
    if (!id) {
      clearEvents();
    } else if (events && id === selectedId) {
      renderEvents(events);
    }
  } finally {
    polling = false;
  }
}

const stateChipClass = {
  Submitted: "wl-chip-warn",
  Paid: "wl-chip-warn",
  Shipped: "wl-chip-warn",
  Completed: "wl-chip-pass",
  Cancelled: "wl-chip-block",
};

function renderOrders(orders) {
  ordersEmpty.hidden = orders.length > 0;
  const rows = [];
  for (const order of orders) {
    const row = document.createElement("tr");
    row.dataset.order = order.orderNumber ?? "";
    row.dataset.state = order.currentState ?? "";
    row.dataset.id = order.correlationId;
    if (order.correlationId === selectedId) {
      row.classList.add("selected");
    }

    const orderCell = document.createElement("td");
    orderCell.textContent = order.orderNumber ?? "";

    const stateCell = document.createElement("td");
    const stateChip = document.createElement("span");
    stateChip.className = `state wl-chip ${stateChipClass[order.currentState] ?? ""}`.trim();
    stateChip.textContent = order.currentState ?? "";
    stateCell.append(stateChip);

    const actions = document.createElement("td");
    actions.className = "actions";
    for (const command of order.commands ?? []) {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "wl-btn wl-btn-ghost wl-btn-sm";
      button.dataset.command = command;
      button.textContent = command;
      actions.append(button);
    }

    row.append(orderCell, stateCell, actions);
    rows.push(row);
  }

  ordersBody.replaceChildren(...rows);
}

function renderEvents(events) {
  fill(sentList, events.sentToSaga);
  fill(receivedList, events.receivedFromSaga);
}

function clearEvents() {
  sentList.replaceChildren();
  receivedList.replaceChildren();
}

function fill(list, names) {
  const items = (names ?? []).map((name) => {
    const item = document.createElement("li");
    item.textContent = name;
    return item;
  });
  list.replaceChildren(...items);
}

refresh();
setInterval(refresh, 1000);
