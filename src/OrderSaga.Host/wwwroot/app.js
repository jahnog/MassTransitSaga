const ordersBody = document.querySelector("#orders");
const sentList = document.querySelector("#sent");
const receivedList = document.querySelector("#received");
const orderNumberInput = document.querySelector("#order-number");

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
    return;
  }

  const created = await response.json();
  selectedId = created.correlationId;
  orderNumberInput.value = "";
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
    await fetch(`/orders/${selectedId}/events`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name: button.dataset.command }),
    });
  }

  await refresh();
});

async function refresh() {
  if (polling) {
    return;
  }

  polling = true;
  try {
    const response = await fetch("/orders");
    if (!response.ok) {
      return;
    }

    renderOrders(await response.json());
    await renderEvents();
  } finally {
    polling = false;
  }
}

function renderOrders(orders) {
  ordersBody.replaceChildren();
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
    stateCell.className = "state";
    stateCell.textContent = order.currentState ?? "";

    const actions = document.createElement("td");
    for (const command of order.commands ?? []) {
      const button = document.createElement("button");
      button.type = "button";
      button.dataset.command = command;
      button.textContent = command;
      actions.append(button);
    }

    row.append(orderCell, stateCell, actions);
    ordersBody.append(row);
  }
}

async function renderEvents() {
  sentList.replaceChildren();
  receivedList.replaceChildren();
  if (!selectedId) {
    return;
  }

  const response = await fetch(`/orders/${selectedId}/events`);
  if (!response.ok) {
    return;
  }

  const events = await response.json();
  fill(sentList, events.sentToSaga);
  fill(receivedList, events.receivedFromSaga);
}

function fill(list, names) {
  for (const name of names ?? []) {
    const item = document.createElement("li");
    item.textContent = name;
    list.append(item);
  }
}

refresh();
setInterval(refresh, 1000);
