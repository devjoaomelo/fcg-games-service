# FCG.Games

> microsserviço gerenciamento de jogos — .NET 8 com Minimal APIs, Clean Architecture e busca avançada

##  Sobre o Projeto

A API Games é o coração do catálogo da plataforma FIAP Cloud Games, oferecendo:

-  CRUD completo de jogos
-  Busca textual avançada com OpenSearch
-  Métricas e agregações
-  Event Sourcing (timeline de eventos por jogo)
-  Integração com API de Payments
-  Observabilidade completa (logs, métricas e tracing)

##  Arquitetura


###  Infraestrutura

- **Runtime:** ECS Fargate
- **Cluster:** fcg-cluster
- **Service:** fcg-games-svc
- **Load Balancer:** app/alb-fcg-games/9ca936ff06163857
- **Target Group:** targetgroup/tg-fcg-games-8080/d041630c1b588d5b
- **Logs:** /ecs/fcg-games (CloudWatch Logs)
- **Tracing:** ADOT sidecar → AWS X-Ray (service name: FCG.Games.Api)

##  Endpoints

### Health Checks
```bash
# Health
GET /health

# Verifica MySQL
GET /health/db
```

### Gerenciamento de Jogos
```bash
# Criar novo jogo
POST /api/games
Content-Type: application/json

{
  "title": "The Last of Us Part II",
  "genre": "Ação/Aventura",
  "price": 199.90,
  "description": "Uma jornada épica...",
  "releaseDate": "2020-06-19"
}

# Buscar jogo por ID
GET /api/games/{id}
```

### Busca e Métricas
```bash
# Busca textual (requer OpenSearch ativo)
GET /api/games/search?q={query}&page=1&size=10

# Métricas agregadas
GET /api/games/metrics

# Reindexar jogos (MySQL → OpenSearch)
POST /api/games/reindex
```

### Event Sourcing
```bash
# Timeline de eventos do jogo
GET /api/games/{id}/events
```

###  Exemplos Práticos

```bash
# Health check
curl -s http://HOST/games/health

# Criar jogo
curl -s -X POST http://HOST/api/games \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Cyberpunk 2077",
    "description": "Uma aventura de mundo aberto",
    "price": 249,
    
  }'

# Buscar jogos de ação
curl -s "http://HOST/api/games/search?q=acao&page=1&size=10"

# Reindexar catálogo
curl -s -X POST http://HOST/api/games/reindex

# Ver eventos de um jogo
curl -s http://HOST/api/games/{GAME_ID}/events

# Métricas do catálogo
curl -s http://HOST/api/games/metrics
```

## 🔧 Configuração

### Variáveis de Ambiente

| Variável | Descrição | Valor (AWS) | Valor (Local) |
|----------|-----------|-------------|---------------|
| ASPNETCORE_URLS | Endereço de binding | http://+:8080 | http://+:8080 |
| ConnectionStrings__GamesDb | Connection string MySQL | SSM Parameter | Server=mysql-games;... |
| Jwt__Key | Chave JWT (compartilhada) | SSM Parameter | dev-secret-key |
| Search__UseOpenSearch | Ativar busca OpenSearch | false | true |
| OpenSearch__Url | URL do OpenSearch | N/A | http://opensearch:9200 |
| OpenSearch__Index | Nome do índice | N/A | games |
| OTEL_EXPORTER_OTLP_ENDPOINT | Endpoint OTLP | http://127.0.0.1:4317 | http://127.0.0.1:4317 |
| OTEL_EXPORTER_OTLP_PROTOCOL | Protocolo OTLP | grpc | grpc |
| OTEL_SERVICE_NAME | Nome do serviço | FCG.Games.Api | FCG.Games.Api |

###  Secrets (AWS Systems Manager)

```
arn:aws:ssm:us-east-2:536765581095:parameter/fcg/games/ConnectionStrings__GamesDb
arn:aws:ssm:us-east-2:536765581095:parameter/fcg/users/Jwt__Key (compartilhado)
```

##  Desenvolvimento Local

### Pré-requisitos
- Docker & Docker Compose
- .NET 8 SDK

### Executar com Docker

```bash
# Subir todos os serviços (MySQL, OpenSearch e API)
docker compose up -d mysql-games opensearch games

# API disponível em: http://localhost:8082
# OpenSearch Dashboard: http://localhost:9200
# MySQL exposto na porta: 3317
```

### Ativar busca com OpenSearch

```bash
# Definir variável de ambiente
export Search__UseOpenSearch=true

# Ou no docker-compose.yml
environment:
  - Search__UseOpenSearch=true
```

### Executar localmente (sem Docker)

```bash
# Restaurar dependências
dotnet restore

# Compilar
dotnet build --configuration Release

# Executar
dotnet run --project src/FCG.Games.Api

# Executar testes
dotnet test --configuration Release --logger trx
```

##  OpenSearch

### Estrutura do Índice

```json
{
  "games": {
    "mappings": {
      "properties": {
        "id": { "type": "keyword" },
        "title": { 
          "type": "text",
          "analyzer": "standard"
        },
        "description": { "type": "text" },
        "price": { "type": "double" },
      }
    }
  }
}
```

### Reindexação

Sempre que houver mudanças no catálogo MySQL, execute:

```bash
POST /api/games/reindex
```

Isso sincroniza todos os jogos do MySQL para o OpenSearch.

##  Observabilidade

### Logs Estruturados

- **Formato:** JSON (Serilog)
- **Destino:** CloudWatch Logs
- **Log Group:** /ecs/fcg-games
- **Nível:** Information (produção), Debug (desenvolvimento)

### Tracing Distribuído

- **Stack:** OpenTelemetry → OTLP → ADOT Collector → AWS X-Ray
- **Service Name:** FCG.Games.Api
- **Integração:** Rastreamento de chamadas para MySQL, OpenSearch e APIs externas

**Visualizar traces:**
```
https://us-east-2.console.aws.amazon.com/xray/home?region=us-east-2#/service-map
```

### Métricas & Alertas

**Dashboard CloudWatch**

<img width="1910" height="791" alt="Captura de tela 2025-10-10 220013" src="https://github.com/user-attachments/assets/45cf9d40-9e66-42b2-a15e-2cd752eacbec" />

**Alarme de Health:**
```
arn:aws:cloudwatch:us-east-2:536765581095:alarm:FCG-Games-Health
```

## Deploy

### Task Definition (ECS Fargate)

A task definition contém:
- **Container principal:** FCG.Games.Api (porta 8080)
- **Sidecar:** adot-collector (porta 4317)
- **Log Driver:** awslogs → /ecs/fcg-games
- **Task Role:** Permissões para X-Ray e CloudWatch

### CI - Pipeline de Testes

```yaml
name: CI - Games API
on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    
    services:
      mysql:
        image: mysql:8.0
        env:
          MYSQL_ROOT_PASSWORD: root
          MYSQL_DATABASE: fcg_games_test
        ports:
          - 3306:3306
        options: >-
          --health-cmd="mysqladmin ping"
          --health-interval=10s
          --health-timeout=5s
          --health-retries=3
    
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      
      - name: Restore
        run: dotnet restore
      
      - name: Build
        run: dotnet build --configuration Release --no-restore
      
      - name: Test
        run: dotnet test --configuration Release --no-build --logger trx
        env:
          ConnectionStrings__GamesDb: "Server=localhost;Database=fcg_games_test;Uid=root;Pwd=root;"
```

### CD - Deploy Automático

```yaml
name: CD - Deploy Games API
on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Configure AWS Credentials
        uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-region: us-east-2
          role-to-assume: arn:aws:iam::536765581095:role/ecsTaskExecutionRole
      
      - name: Login to ECR
        run: |
          aws ecr get-login-password --region us-east-2 | \
            docker login --username AWS --password-stdin $ECR_REPO
      
      - name: Build and Push
        run: |
          docker build -t $ECR_REPO:games-$GITHUB_SHA .
          docker push $ECR_REPO:games-$GITHUB_SHA
      
      - name: Update ECS Service
        run: |
          aws ecs update-service \
            --cluster fcg-cluster \
            --service fcg-games-svc \
            --force-new-deployment
```

##  Integrações

### API de Payments

A Games API integra com a API de Payments para processar compras:

```csharp
// Exemplo de integração
POST https://payments.fcg.com/api/payments
{
  "gameId": "123",
  "userId": "456",
  "amount": 199.90
}
```

### Event-Driven Architecture

Publica eventos para filas SQS quando:
-  Nova compra de jogo

###  Alarme de Health vermelho

```bash
# 1. Verificar health endpoints
curl -v http://load-balancer-url/health
curl -v http://load-balancer-url/health/db

# 2. Verificar logs
aws logs tail /ecs/fcg-games --follow

# 3. Verificar target group
aws elbv2 describe-target-health \
  --target-group-arn arn:aws:elasticloadbalancing:us-east-2:...:targetgroup/tg-fcg-games-8080/...
```

**Possíveis causas:**
- Security Group bloqueando porta 8080
- Connection string do MySQL incorreta
- RDS inacessível (subnet/routing)


###  Sem traces no X-Ray

```bash
# Verificar variáveis OTLP
echo $OTEL_EXPORTER_OTLP_ENDPOINT  # deve ser http://127.0.0.1:4317
echo $OTEL_SERVICE_NAME             # deve ser FCG.Games.Api

# Verificar logs do ADOT collector
aws logs tail /ecs/adot-collector --follow

# Verificar IAM role da task
aws iam get-role --role-name ecsTaskRole
```

### Logs ausentes no CloudWatch

```bash
# Verificar log group existe
aws logs describe-log-groups --log-group-name-prefix /ecs/fcg-games

# Verificar permissões da task execution role
aws iam list-attached-role-policies \
  --role-name ecsTaskExecutionRole
```

## Testes

```bash
# Executar todos os testes
dotnet test
```
João Melo - Para FIAP Postech
