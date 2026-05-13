# 🚀 Guía Nivel 5: Despliegue Local con Docker Desktop y Kubernetes

¡Bienvenidos al **Nivel 5**! En esta guía aprenderán cómo levantar todo nuestro ecosistema de microservicios (Órdenes, Inventario, Productos, Notificaciones y Gateway) utilizando **Docker Desktop** con **Kubernetes habilitado**, una base de datos **SQL Server** real y un **RabbitMQ** operando en nuestro propio entorno, completamente sin dependencias de la nube (sin CloudAMQP ni bases de datos externas).

---

## 🛠️ PASO 1: Requisitos Previos (El Entorno del Arquitecto)

1. Tener instalado **Docker Desktop**.
2. En Docker Desktop, ir a **Settings (Engranaje)** > **Kubernetes** > Marcar **"Enable Kubernetes"**.
3. Hacer clic en "Apply & restart" y esperar a que el ícono de Kubernetes en la parte inferior izquierda se ponga verde.
4. (Opcional, pero recomendado) Abre tu terminal y verifica que Kubernetes responde:
   ```bash
   kubectl get nodes
   ```

---

## 🏗️ PASO 2: El Truco de Magia DNS (Archivo Hosts)

Para que nuestro Ingress Controller pueda enrutar el tráfico localmente simulando un dominio real, debemos engañar a nuestra computadora.

1. Abre el bloc de notas (Notepad) **como Administrador**.
2. Abre la ruta: `C:\Windows\System32\drivers\etc\hosts`
3. Agrega esta línea al final del archivo y guárdalo:
   ```text
   127.0.0.1       api.itm-tickets.com
   ```

---

## 🛡️ PASO 3: Instalar "El Portero" (NGINX Ingress Controller)

El Ingress Controller es quien recibe las peticiones de `api.itm-tickets.com` y las reparte a nuestros microservicios.
En tu terminal de PowerShell, ejecuta:

```bash
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.8.2/deploy/static/provider/cloud/deploy.yaml
```
Espera un par de minutos comprobando que se instale con `kubectl get pods -n ingress-nginx`.

---

## 🏭 PASO 4: Construir nuestras Imágenes (Docker Build)

Necesitamos "empacar" nuestros microservicios para que Kubernetes los pueda usar localmente. Ejecuta estos comandos uno a uno en la raíz de la solución (`C:\PDI74-3\Itm.Distributed.System\`):

```bash
docker build -t ds-itm-gateway-api:latest -f Itm.Gateway.Api/Dockerfile .
docker build -t ds-itm-inventory-api:latest -f Itm.Inventory.Api/Dockerfile .
docker build -t ds-itm-product-api:latest -f Itm.Product.Api/Dockerfile .
docker build -t ds-itm-notification-api:latest -f Notification.Api/Dockerfile .
docker build -t ds-itm-order-api:latest -f Order.Api/Dockerfile .
```

---

## 🚀 PASO 5: Desplegar Infraestructura y Bases de Datos Reales

Levantaremos nuestro RabbitMQ local y el servidor de base de datos SQL Server:

```bash
kubectl apply -f sql-server.yaml
kubectl apply -f rabbitmq.yaml
```
*(Espera un minuto para que ambos arranquen, puedes mirar su estado con `kubectl get pods`)*.

---

## 🧩 PASO 6: Desplegar los Microservicios e Ingress

Aplica uno por uno los manifiestos de los microservicios:

```bash
kubectl apply -f gateway-api.yaml
kubectl apply -f inventory-api.yaml
kubectl apply -f product-api.yaml
kubectl apply -f notification-api.yaml
kubectl apply -f order-deployment.yaml
kubectl apply -f order-service.yaml
kubectl apply -f itm-ingress.yaml
```

Vigila que todos cambien a estado **Running**:
```bash
kubectl get pods -w
```
*(Presiona `Ctrl + C` para salir de la vigilancia cuando todos estén listos).*

---

## 🎯 PASO 7: Pruebas Nivel 5 (¡Momento de la Verdad!)

Todo el tráfico entra por el dominio configurado. Puedes usar tu navegador o Postman:

1. **Estado de Salud del Gateway:**
   Ingresa a: `http://api.itm-tickets.com/api/gateway/health`
   *Debes ver un JSON indicando estado `Healthy`.*

2. **Leer toda las Órdenes (Base de datos Local):**
   Ingresa a: `http://api.itm-tickets.com/orders/api/orders`
   *Debes ver un arreglo vacío `[]` (si es tu primera prueba) devuelto directo del EF Core en tu SQL local.*

¡Felicidades! Has completado el entorno real de Kubernetes, base de datos local y RabbitMQ local de Arquitectura de Sistemas Distribuidos. 🚀