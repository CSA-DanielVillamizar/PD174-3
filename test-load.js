import http from 'k6/http';
import { sleep, check } from 'k6';

export const options = {
  stages: [
    { duration: '30s', target: 50 },  // Rampa de subida
    { duration: '1m', target: 100 }, // Meseta de alta carga
    { duration: '30s', target: 0 },   // Rampa de bajada
  ],
  thresholds: {
    http_req_failed: ['rate<0.05'],   // Error rate menor al 5% (tolerancia al encolamiento y RabbitMQ resiliencia)
    http_req_duration: ['p(95)<2000'], // 95% de peticiones < 2000ms debido a la DB
  },
};

export default function () {
  // ATACAR MULTIPLES CONTENEDORES Y SERVICIOS EN EL CLUSTER A TRAVÉS DE INGRESS / GATEWAY

  const params = { headers: { 'Content-Type': 'application/json' } };

  // 1. Atacar al Order Service (Ruta Directa al Ingress)
  const orderUrl = 'http://api.itm-tickets.com/orders/api/orders';
  const orderPayload = JSON.stringify({ productId: 1, quantity: 1 });
  const resOrder = http.post(orderUrl, orderPayload, params);
  check(resOrder, {
    'Order status is 201': (r) => r.status === 201 || r.status === 200,
  });

  // 2. Atacar al Product Service (Enrutado vía YARP Gateway Ingress)
  const prodUrl = 'http://api.itm-tickets.com/api/products/1';
  const resProd = http.get(prodUrl);
  check(resProd, {
    'Product status is 200': (r) => r.status === 200,
  });

  // 3. Atacar al Inventory Service (Enrutado vía YARP Gateway pero usando la ruta expuesta /bodega)
  const invUrl = 'http://api.itm-tickets.com/api/bodega/1'; // El gateway lo transforma a /api/inventory/1
  const resInv = http.get(invUrl);
  check(resInv, {
    'Inventory status is 200': (r) => r.status === 200,
  });

  sleep(1); // "Think time" del usuario real simulando flujos reales
}
