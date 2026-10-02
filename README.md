# Inventory Management System

> This is an older version of my inventory app. I built it as microservices mainly to learn how that
> architecture works in practice. The app we actually use now is the same system rewritten as a
> modular monolith: [Inventory-Management-Modular-Monolith](https://github.com/yusifbagiyev/Inventory-Management-Modular-Monolith).

A microservices-based inventory management system built with .NET 8 for tracking products, managing transfers between departments, and handling approval workflows.

## Features

- **Product Management** - Add, edit, delete products with images and track their status
- **Inventory Transfers** - Move products between departments with full history tracking
- **Approval Workflow** - Request and approve changes for controlled environments
- **Real-time Notifications** - Get instant updates via SignalR and WhatsApp integration
- **Role-based Access** - Admin, Manager, and User roles with fine-grained permissions
- **Department & Category Management** - Organize products by location and type
- **Search & Filtering** - Find products quickly with Azerbaijani language support
- **Export Options** - Generate PDF and Word reports

## Architecture

The system uses a microservices architecture:

| Service | Description |
|---------|-------------|
| API Gateway | Routes requests using Ocelot |
| Identity Service | Handles authentication and user management |
| Product Service | Manages products, categories, and departments |
| Route Service | Tracks inventory movements and transfers |
| Approval Service | Manages approval requests and workflows |
| Notification Service | Real-time notifications via SignalR |
| Web Application | User interface |

## Tech Stack

- **.NET 8** - Backend framework
- **PostgreSQL** - Database
- **RabbitMQ** - Message broker for service communication
- **SignalR** - Real-time notifications
- **Entity Framework Core** - ORM
- **MediatR** - CQRS pattern implementation
- **Docker** - Containerization
- **Nginx** - Reverse proxy with SSL
- **Serilog + Seq** - Centralized logging
- **JWT** - Authentication

## Getting Started

### Prerequisites

- Docker and Docker Compose
- .NET 8 SDK (for local development)

### Running with Docker

1. Clone the repository
```bash
git clone https://github.com/yourusername/inventory-management.git
cd inventory-management
```

2. Create a `.env` file with your configuration:
```env
DB_USER=postgres
DB_PASSWORD=your_password
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest
JWT_SECRET_KEY=your_secret_key
JWT_ISSUER=your_issuer
JWT_AUDIENCE=your_audience
SEQ_ADMIN_PASSWORD=your_password
```

3. Start the services
```bash
docker-compose up -d
```

4. Access the application at `https://localhost` and sign in with the seeded demo admin (`admin` / `Admin12345`)

### Local Development

Each service can be run individually:

```bash
cd src/ProductService.API
dotnet run
```

Development ports:
- Web App: 5051
- API Gateway: 5000
- Product Service: 5001
- Route Service: 5002
- Identity Service: 5003
- Approval Service: 5004
- Notification Service: 5005
- Seq Dashboard: 5342

## Project Structure

```
├── src/
│   ├── ApiGateway/
│   ├── IdentityService.API/
│   ├── IdentityService.Application/
│   ├── IdentityService.Domain/
│   ├── IdentityService.Infrastructure/
│   ├── ProductService.API/
│   ├── ProductService.Application/
│   ├── ProductService.Domain/
│   ├── ProductService.Infrastructure/
│   ├── RouteService.API/
│   ├── RouteService.Application/
│   ├── RouteService.Domain/
│   ├── RouteService.Infrastructure/
│   ├── ApprovalService.API/
│   ├── ApprovalService.Application/
│   ├── ApprovalService.Domain/
│   ├── ApprovalService.Infrastructure/
│   ├── NotificationService.API/
│   ├── NotificationService.Application/
│   ├── NotificationService.Domain/
│   ├── NotificationService.Infrastructure/
│   ├── InventoryManagement.Web/
│   └── SharedServices/
├── docker-compose.yml
└── nginx.conf
```

## API Documentation

When running in development mode, Swagger documentation is available at:
- Product Service: `http://localhost:5001/swagger`
- Route Service: `http://localhost:5002/swagger`
- Identity Service: `http://localhost:5003/swagger`
- Approval Service: `http://localhost:5004/swagger`
- Notification Service: `http://localhost:5005/swagger`

