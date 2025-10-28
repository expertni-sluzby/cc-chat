# Chat Server Deployment Guide

## Overview

This guide covers deploying the Chat Server to various environments, from local development to production cloud deployments.

**Supported Deployment Methods:**
- Docker / Docker Compose
- Kubernetes
- Azure App Service
- AWS Elastic Beanstalk
- Linux VM / Bare Metal

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Development](#local-development)
3. [Docker Deployment](#docker-deployment)
4. [Kubernetes Deployment](#kubernetes-deployment)
5. [Cloud Deployments](#cloud-deployments)
6. [Production Checklist](#production-checklist)
7. [Monitoring & Logging](#monitoring--logging)
8. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required
- .NET 9.0 SDK (for building)
- Docker 20+ (for containerized deployments)
- Git

### Optional
- Kubernetes cluster (for K8s deployment)
- Azure/AWS account (for cloud deployment)
- Redis instance (for horizontal scaling)

---

## Local Development

### Option 1: Direct .NET Runtime

```bash
# 1. Clone repository
git clone https://github.com/yourusername/cc-chat.git
cd cc-chat

# 2. Restore dependencies
dotnet restore

# 3. Run tests
dotnet test

# 4. Run server
dotnet run --project src/ChatServer

# Server available at:
# - HTTP: http://localhost:5000
# - Swagger: http://localhost:5000/swagger
```

### Option 2: Docker Compose (Development)

```bash
# Build and run
docker-compose -f docker-compose.dev.yml up --build

# Run in background
docker-compose -f docker-compose.dev.yml up -d

# View logs
docker-compose -f docker-compose.dev.yml logs -f

# Stop
docker-compose -f docker-compose.dev.yml down
```

---

## Docker Deployment

### Single Container (Production)

#### 1. Build Image

```bash
# Build production image
docker build -t chatserver:latest .

# Or with specific tag
docker build -t chatserver:1.0.0 .
```

#### 2. Run Container

```bash
# Run with default settings
docker run -d \
  --name chatserver \
  -p 5000:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  chatserver:latest

# Run with custom configuration
docker run -d \
  --name chatserver \
  -p 5000:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://+:8080 \
  --restart unless-stopped \
  chatserver:latest
```

#### 3. Verify Deployment

```bash
# Check container status
docker ps

# Check logs
docker logs chatserver

# Test health endpoint
curl http://localhost:5000/health
```

### Docker Compose (Production)

#### 1. Configure docker-compose.yml

```yaml
version: '3.8'

services:
  chatserver:
    image: chatserver:latest
    container_name: chatserver-prod
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
    restart: always
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 3s
      retries: 3
```

#### 2. Deploy

```bash
# Start services
docker-compose up -d

# Check status
docker-compose ps

# View logs
docker-compose logs -f chatserver

# Update deployment
docker-compose pull
docker-compose up -d

# Stop services
docker-compose down
```

---

## Kubernetes Deployment

### Architecture

```
┌──────────────────────────────────────┐
│         Ingress Controller           │
│      (NGINX / Traefik)               │
└─────────┬────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────┐
│         Service                      │
│      (LoadBalancer)                  │
└─────────┬───────────────────────────┘
          │
     ┌────┴────┐
     ▼         ▼
┌─────────┐ ┌─────────┐
│  Pod 1  │ │  Pod 2  │
│ChatServer│ │ChatServer│
└─────────┘ └─────────┘
```

### 1. Create Kubernetes Manifests

#### deployment.yaml

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: chatserver
  labels:
    app: chatserver
spec:
  replicas: 2
  selector:
    matchLabels:
      app: chatserver
  template:
    metadata:
      labels:
        app: chatserver
    spec:
      containers:
      - name: chatserver
        image: chatserver:latest
        ports:
        - containerPort: 8080
          name: http
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ASPNETCORE_URLS
          value: "http://+:8080"
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 10
```

#### service.yaml

```yaml
apiVersion: v1
kind: Service
metadata:
  name: chatserver
spec:
  type: LoadBalancer
  selector:
    app: chatserver
  ports:
  - name: http
    port: 80
    targetPort: 8080
    protocol: TCP
  sessionAffinity: ClientIP  # Required for SignalR
  sessionAffinityConfig:
    clientIP:
      timeoutSeconds: 3600
```

#### ingress.yaml (Optional with HTTPS)

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: chatserver-ingress
  annotations:
    kubernetes.io/ingress.class: nginx
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/affinity: "cookie"  # For SignalR
    nginx.ingress.kubernetes.io/session-cookie-name: "route"
spec:
  tls:
  - hosts:
    - chat.yourdomain.com
    secretName: chatserver-tls
  rules:
  - host: chat.yourdomain.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: chatserver
            port:
              number: 80
```

### 2. Deploy to Kubernetes

```bash
# Apply manifests
kubectl apply -f deployment.yaml
kubectl apply -f service.yaml
kubectl apply -f ingress.yaml

# Check deployment
kubectl get deployments
kubectl get pods
kubectl get services

# View logs
kubectl logs -f deployment/chatserver

# Scale deployment
kubectl scale deployment chatserver --replicas=3

# Update image
kubectl set image deployment/chatserver chatserver=chatserver:1.0.1

# Rollback
kubectl rollout undo deployment/chatserver
```

### 3. Important: SignalR Configuration

For multi-pod deployments, you MUST use sticky sessions:

**Option 1: Session Affinity (Recommended for small scale)**
```yaml
# In service.yaml
sessionAffinity: ClientIP
```

**Option 2: Redis Backplane (Recommended for production)**
```csharp
// In Program.cs
builder.Services.AddSignalR()
    .AddStackExchangeRedis(configuration.GetConnectionString("Redis"));
```

---

## Cloud Deployments

### Azure App Service

#### 1. Create App Service

```bash
# Login to Azure
az login

# Create resource group
az group create --name chatserver-rg --location eastus

# Create App Service plan
az appservice plan create \
  --name chatserver-plan \
  --resource-group chatserver-rg \
  --sku B1 \
  --is-linux

# Create web app
az webapp create \
  --name chatserver-app \
  --resource-group chatserver-rg \
  --plan chatserver-plan \
  --runtime "DOTNET|9.0"
```

#### 2. Configure App Settings

```bash
# Set environment variables
az webapp config appsettings set \
  --name chatserver-app \
  --resource-group chatserver-rg \
  --settings ASPNETCORE_ENVIRONMENT=Production

# Enable WebSocket
az webapp config set \
  --name chatserver-app \
  --resource-group chatserver-rg \
  --web-sockets-enabled true
```

#### 3. Deploy Code

**Option A: GitHub Actions**
```yaml
# .github/workflows/deploy.yml
name: Deploy to Azure

on:
  push:
    branches: [ main ]

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '9.0.x'

    - name: Build
      run: dotnet build -c Release

    - name: Publish
      run: dotnet publish -c Release -o ./publish

    - name: Deploy to Azure
      uses: azure/webapps-deploy@v2
      with:
        app-name: chatserver-app
        publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
        package: ./publish
```

**Option B: Azure CLI**
```bash
# Publish locally
dotnet publish -c Release -o ./publish

# Deploy
az webapp deploy \
  --name chatserver-app \
  --resource-group chatserver-rg \
  --src-path ./publish.zip \
  --type zip
```

#### 4. Configure Custom Domain & SSL

```bash
# Add custom domain
az webapp config hostname add \
  --webapp-name chatserver-app \
  --resource-group chatserver-rg \
  --hostname chat.yourdomain.com

# Enable HTTPS only
az webapp update \
  --name chatserver-app \
  --resource-group chatserver-rg \
  --https-only true

# Bind SSL certificate (managed certificate)
az webapp config ssl create \
  --name chatserver-app \
  --resource-group chatserver-rg \
  --hostname chat.yourdomain.com
```

### AWS Elastic Beanstalk

#### 1. Install EB CLI

```bash
pip install awsebcli
```

#### 2. Initialize EB Application

```bash
# Initialize
eb init -p docker chatserver

# Create environment
eb create chatserver-prod \
  --instance-type t3.medium \
  --envvars ASPNETCORE_ENVIRONMENT=Production
```

#### 3. Deploy

```bash
# Deploy
eb deploy

# Open in browser
eb open

# View logs
eb logs

# SSH into instance
eb ssh
```

#### 4. Configure Load Balancer

```bash
# Enable sticky sessions (required for SignalR)
eb config

# Add to .ebextensions/loadbalancer.config:
```

```yaml
option_settings:
  aws:elb:policies:
    Stickiness Policy: true
    Stickiness Cookie Expiration: 3600
```

### Digital Ocean / Other VPS

#### 1. Setup Server

```bash
# SSH into server
ssh root@your-server-ip

# Update system
apt update && apt upgrade -y

# Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sh get-docker.sh

# Install Docker Compose
apt install docker-compose -y
```

#### 2. Deploy Application

```bash
# Clone repository
git clone https://github.com/yourusername/cc-chat.git
cd cc-chat

# Build and run
docker-compose up -d

# Setup nginx reverse proxy (optional)
apt install nginx -y
```

#### 3. Configure Nginx Reverse Proxy

```nginx
# /etc/nginx/sites-available/chatserver
server {
    listen 80;
    server_name chat.yourdomain.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

```bash
# Enable site
ln -s /etc/nginx/sites-available/chatserver /etc/nginx/sites-enabled/
nginx -t
systemctl reload nginx

# Setup SSL with Let's Encrypt
apt install certbot python3-certbot-nginx -y
certbot --nginx -d chat.yourdomain.com
```

---

## Production Checklist

### Security

- [ ] Enable HTTPS/WSS (valid SSL certificate)
- [ ] Configure CORS to restrict origins
- [ ] Implement rate limiting
- [ ] Add JWT authentication (recommended)
- [ ] Enable HSTS headers
- [ ] Configure firewall rules
- [ ] Use secrets management (Azure Key Vault, AWS Secrets Manager)
- [ ] Regular security updates

### Performance

- [ ] Enable HTTP/2
- [ ] Configure response compression
- [ ] Set up CDN (if serving static content)
- [ ] Optimize connection pool settings
- [ ] Configure SignalR limits
- [ ] Enable output caching (where appropriate)

### Reliability

- [ ] Configure health checks
- [ ] Set up auto-scaling
- [ ] Implement graceful shutdown
- [ ] Configure retry policies
- [ ] Set up backup strategy (if adding persistence)
- [ ] Test failover scenarios

### Monitoring

- [ ] Set up application monitoring (Application Insights, Datadog)
- [ ] Configure logging aggregation (ELK, Seq)
- [ ] Set up alerts (CPU, memory, errors)
- [ ] Monitor SignalR connection metrics
- [ ] Track API response times
- [ ] Monitor error rates

### Documentation

- [ ] Document deployment process
- [ ] Create runbook for common issues
- [ ] Document rollback procedures
- [ ] Maintain change log
- [ ] Document environment variables

---

## Environment Variables

### Required

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment name | `Development` |
| `ASPNETCORE_URLS` | Listening URLs | `http://+:5000` |

### Optional

| Variable | Description | Default |
|----------|-------------|---------|
| `Logging__LogLevel__Default` | Log level | `Information` |
| `ConnectionStrings__Redis` | Redis connection (if using) | - |
| `CORS__AllowedOrigins` | CORS origins | `*` (dev only) |

### Example .env File

```bash
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
Logging__LogLevel__Default=Information
Logging__LogLevel__Microsoft.AspNetCore=Warning
```

---

## Monitoring & Logging

### Application Insights (Azure)

```csharp
// In Program.cs
builder.Services.AddApplicationInsightsTelemetry();
```

```bash
# Set instrumentation key
az webapp config appsettings set \
  --name chatserver-app \
  --resource-group chatserver-rg \
  --settings APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=..."
```

### Structured Logging (Serilog)

```bash
# Install packages
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Seq
```

```csharp
// In Program.cs
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console()
        .WriteTo.Seq("http://seq-server:5341");
});
```

### Health Checks Dashboard

The application exposes `/health` endpoint:

```json
GET /health
{
  "status": "healthy",
  "timestamp": "2025-10-29T12:00:00Z"
}
```

---

## Troubleshooting

### WebSocket Connection Fails

**Symptoms:**
- SignalR fails to connect
- Falls back to long polling

**Solutions:**
1. Ensure WebSockets are enabled on server
2. Check reverse proxy configuration (nginx upgrade headers)
3. Verify firewall allows WebSocket connections
4. Check CORS configuration

```bash
# Azure: Enable WebSocket
az webapp config set --web-sockets-enabled true

# Nginx: Add upgrade headers (see nginx config above)
```

### High Memory Usage

**Symptoms:**
- Container OOM kills
- Slow response times

**Solutions:**
1. Check for memory leaks in connection cleanup
2. Limit message history size
3. Implement message TTL
4. Scale horizontally instead of vertically

```csharp
// Limit message history
private const int MaxMessagesPerRoom = 1000;
```

### SignalR Disconnections

**Symptoms:**
- Frequent reconnections
- Lost messages

**Solutions:**
1. Configure keep-alive settings
2. Implement reconnection logic on client
3. Check network stability
4. Enable sticky sessions (load balancer)

```csharp
// In Program.cs
builder.Services.AddSignalR(options =>
{
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});
```

### Deployment Fails

**Common Issues:**

1. **Port already in use**
   ```bash
   # Check what's using the port
   lsof -i :5000
   # Kill process
   kill -9 <PID>
   ```

2. **Docker build fails**
   ```bash
   # Clear Docker cache
   docker system prune -a
   # Rebuild
   docker build --no-cache -t chatserver:latest .
   ```

3. **Health check fails**
   ```bash
   # Test health endpoint locally
   docker exec chatserver curl http://localhost:8080/health
   ```

---

## Scaling

### Vertical Scaling

Increase resources for single instance:

```bash
# Docker: Increase memory limit
docker run -d --memory="2g" --cpus="2" chatserver:latest

# Kubernetes: Update resources
kubectl set resources deployment chatserver \
  --limits=cpu=1000m,memory=1Gi \
  --requests=cpu=500m,memory=512Mi
```

### Horizontal Scaling

Multiple instances (requires Redis backplane):

```bash
# Kubernetes
kubectl scale deployment chatserver --replicas=5

# Docker Compose
docker-compose up --scale chatserver=3
```

**Required: Redis Backplane**
```csharp
// Install: Microsoft.AspNetCore.SignalR.StackExchangeRedis
builder.Services.AddSignalR()
    .AddStackExchangeRedis("redis-connection-string");
```

---

## Backup & Disaster Recovery

### Current Architecture (In-Memory)

**Important:** All data is lost on restart!

For production, consider:

1. **Add persistence layer**
   - PostgreSQL / SQL Server for permanent storage
   - Redis for session/cache

2. **Implement backup strategy**
   - Regular database backups
   - Configuration backups
   - Docker image versioning

3. **Disaster recovery plan**
   - Document recovery procedures
   - Test restore process
   - Maintain off-site backups

---

## Cost Optimization

### Development
- Use smallest instance size
- Stop services when not in use
- Use free tiers (Azure F1, AWS Free Tier)

### Production
- Right-size instances based on metrics
- Use reserved instances (save 30-70%)
- Implement auto-scaling (scale down at night)
- Use spot instances for non-critical workloads

**Estimated Monthly Costs:**

| Platform | Small (1-100 users) | Medium (100-1000) | Large (1000+) |
|----------|---------------------|-------------------|---------------|
| Azure App Service | $13 (B1) | $55 (S1) | $220 (P1v2) |
| AWS Elastic Beanstalk | $15 (t3.small) | $60 (t3.medium) | $240 (t3.large) |
| Digital Ocean | $12 (Basic) | $48 (General Purpose) | $96+ (CPU-Optimized) |
| Kubernetes (AKS/EKS) | $75+ (cluster + nodes) | $150+ | $300+ |

---

## Support & Maintenance

### Upgrade Process

1. Test upgrade in staging environment
2. Review breaking changes in release notes
3. Update dependencies
4. Run test suite
5. Deploy to production with rollback plan
6. Monitor for issues

### Maintenance Windows

Recommended schedule:
- **Updates:** Weekly (security patches)
- **Major upgrades:** Quarterly
- **Dependency updates:** Monthly

---

## Additional Resources

- [ASP.NET Core Deployment](https://docs.microsoft.com/aspnet/core/host-and-deploy/)
- [SignalR Scale Out](https://docs.microsoft.com/aspnet/core/signalr/scale)
- [Docker Best Practices](https://docs.docker.com/develop/dev-best-practices/)
- [Kubernetes Production Best Practices](https://kubernetes.io/docs/setup/best-practices/)

---

**Last Updated:** 2025-10-29
**Version:** 1.0
