# 🏦 Teste Prático: Sistema de Analytics de Portfólio

## 1. Implementação e uso

API .NET 8 para analisar performance, risco e rebalanceamento de carteiras pré-carregadas. Os dados são somente leitura e vêm de `Data/SeedData.json`; não há CRUD, autenticação ou banco persistente nesta entrega.

### Pré-requisitos

- .NET SDK 8.0 ou superior compatível com `net8.0`.
- Abra o PowerShell na pasta do projeto antes de executar os comandos:

```powershell
cd C:\GmDev\Portfolio-Financeiro-gmjr
```

### Executar a API

```powershell
dotnet restore
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project rsc/Portfolio-Financeiro-WebApplication/PortfolioAnalytics.WebApi.csproj
```

Na inicialização, `Program.cs` cria o banco EF Core InMemory e carrega automaticamente `Data/SeedData.json`. O console informa a URL de escuta; em ambiente `Development`, o Swagger está disponível em `{url}/swagger`.

### Executar os testes

```powershell
dotnet test Portfolio-Financeiro.slnx
```

Para executar apenas os principais grupos:

```powershell
dotnet test tests/Application.Tests/Application.Tests.csproj
dotnet test tests/Api.Tests/Api.Tests.csproj
dotnet test tests/DAL.Tests/Persistence.Tests.csproj
```

### Endpoints

| Endpoint | Resultado |
|---|---|
| `GET /api/portfolios/{id}/performance` | Capital inicial, valor atual, retorno, retorno anualizado, volatilidade e performance por posição. |
| `GET /api/portfolios/{id}/risk-analysis` | Nível de risco, Sharpe, concentração, diversificação setorial e recomendações. |
| `GET /api/portfolios/{id}/rebalancing` | Alocações atuais, operações autofinanciadas, custos e impacto esperado na concentração. |

Os IDs das três carteiras seed são `1`, `2` e `3`. IDs não positivos retornam `400`, carteiras inexistentes retornam `404`, e dados de carteira incompletos retornam `422` com `ProblemDetails`.

Exemplo de chamada, depois de substituir `{url}` pelo endereço informado no console:

```powershell
Invoke-RestMethod "{url}/api/portfolios/1/performance"
Invoke-RestMethod "{url}/api/portfolios/1/risk-analysis"
Invoke-RestMethod "{url}/api/portfolios/1/rebalancing"
```

### Arquitetura e responsabilidades

- `Portfolio-Financeiro-WebApplication`: controller HTTP, Swagger e composição da aplicação.
- `Application`: casos de uso e algoritmos puros de performance, risco e rebalanceamento.
- `Models`: agregados `Portfolio` e `Asset`, posições e regras de negócio.
- `DAL`: EF Core InMemory, repositórios, queries e semeadura do JSON.
- `IoC`: registro de dependências.
- `tests`: testes unitários, de persistência/integração e de API.

O cálculo de rebalanceamento depende de `IRebalancingOptimizer`; o caso de uso apenas lê os dados e apresenta a resposta. Essa separação mantém o algoritmo independente de HTTP, EF Core e logging.

### Fórmulas financeiras e premissas

- **Retorno total:** `((valor atual - valor investido) / valor investido) × 100`. `totalInvestment` é o capital inicial de `Portfolio.TotalInvestment`, mesmo quando a soma das posições é diferente.
- **Retorno anualizado:** `((1 + retorno total / 100)^(365 / dias decorridos) - 1) × 100`.
- **Peso da posição:** `(valor de mercado da posição / valor de mercado total) × 100`.
- **Retorno diário:** `(fechamento[t] - fechamento[t-1]) / fechamento[t-1]`.
- **Volatilidade de performance:** desvio-padrão populacional dos retornos diários ponderados; é retornada em base diária.
- **Volatilidade usada no Sharpe:** volatilidade diária anualizada por `√252`.
- **Sharpe Ratio:** `(retorno anualizado - Selic anual) / volatilidade anualizada`.
- **Custo de transação:** `valor negociado × 0,3%`, arredondado comercialmente para centavos.
- **Rebalanceamento:** o valor-alvo é `valor pós-custos × peso-alvo`; o plano resolve `valor pós-custos + custos = valor atual`, para que vendas líquidas financiem compras e taxas. A quantidade é `valor negociado / preço atual`.

### Regras de negócio e edge cases

- Cada `TargetAllocation` deve estar entre 0% e 100%; carteiras com posições devem somar 100%, com tolerância de 0,0001%. Violações lançam `BusinessViolationException`.
- O otimizador normaliza targets como proteção adicional para dados externos/legados, mas a entidade `Portfolio` rejeita esse estado na criação ou alteração normal.
- Uma cotação duplicada no mesmo dia é consolidada pela cotação mais recente, que passa a ser o fechamento diário.
- Sem histórico suficiente, histórico parcial, divisão por zero, preço atual zero ou datas inválidas: a métrica que não pode ser calculada retorna `null` ou não gera trade, conforme o endpoint.
- Trades só são sugeridos para desvios maiores que 2 pontos percentuais e valores de pelo menos R$100. Operações são ordenadas pelo maior desvio e o plano evita aporte externo.
- Alocações/ativos ausentes para uma carteira conhecida são tratados como dados incompletos (`422`).

### Observabilidade

Em `Development`, a categoria `Application` está configurada em nível `Debug`. Os casos de uso registram, com `Operation` e `PortfolioId`, o carregamento dos dados, entradas relevantes, resultados dos cálculos e operações de rebalanceamento. Os algoritmos puros não recebem logger, preservando testabilidade e responsabilidade única.

### Estado da entrega

- AnalyticsController com três endpoints funcionais, Swagger e testes de integração.
- Seed automático via `IDataSower` na inicialização.
- Algoritmos financeiros, validações, tratamento de dados faltantes e logs estruturados.
- Testes unitários e de integração para cálculos, regras de negócio, persistência e API.

Na última validação local, passaram 38 testes de `Application.Tests`, 14 de `Persistence.Tests` e 13 de `Api.Tests`.

---

## 2. Enunciado original

### 📋 Descrição do Desafio

Desenvolva uma **WebAPI em .NET 8** com foco em **algoritmos financeiros**. Este teste avalia:

- 🧠 **Raciocínio Lógico**: Cálculos financeiros e otimização
- 🔧 **Qualidade de Código**: Clean Code, SOLID, testabilidade
- 📊 **Problem Solving**: Análise de dados complexos e edge cases

⏱️ **Tempo estimado**: 3-4 horas

---

### 🎯 Objetivo

Implementar **3 endpoints analíticos** que processam dados de um portfólio de investimentos pré-carregado.

#### Endpoints a Implementar

##### Analytics Controller

1. **`GET /api/portfolios/{id}/performance`**
   - Retorna métricas de performance do portfólio

2. **`GET /api/portfolios/{id}/risk-analysis`**
   - Analisa risco e diversificação

3. **`GET /api/portfolios/{id}/rebalancing`**
   - Sugere ajustes para otimizar o portfólio

---

### 📊 Dados Fornecidos

Você receberá um arquivo **`SeedData.json`** com:

- **15 ativos** da bolsa brasileira (PETR4, VALE3, ITUB4, etc.)
- **3 portfólios** com diferentes estratégias (Conservador, Crescimento, Dividendos)
- **Histórico de preços** (30 dias) para 5 ativos principais
- **Market data** (Taxa Selic, Ibovespa)

#### Estrutura Simplificada

```
Portfolio
├── Id, Name, UserId
├── TotalInvestment: valor total investido inicialmente
└── Positions[]
    ├── Symbol: código do ativo (ex: "PETR4")
    ├── Quantity: quantidade de ações
    ├── AveragePrice: preço médio de compra
    └── TargetAllocation: % ideal deste ativo no portfólio

Asset
├── Symbol: "PETR4"
├── Name, Type, Sector
├── CurrentPrice: preço atual
└── PriceHistory[]: histórico de 30 dias
```

---

### 📋 Especificações dos Endpoints

#### 1. Performance Analysis
**`GET /api/portfolios/{id}/performance`**

Calcule e retorne:

```json
{
  "totalInvestment": 100000.00,
  "currentValue": 108500.50,
  "totalReturn": 8.50,
  "totalReturnAmount": 8500.50,
  "annualizedReturn": 12.34,
  "volatility": 15.67,
  "positionsPerformance": [
    {
      "symbol": "PETR4",
      "investedAmount": 10000.00,
      "currentValue": 11200.00,
      "return": 12.00,
      "weight": 10.32
    }
  ]
}
```

**Algoritmos Necessários:**
- **Total Return**: `(ValorAtual - ValorInvestido) / ValorInvestido * 100`
- **Annualized Return**: `((1 + TotalReturn)^(365/dias) - 1) * 100`
- **Volatility**: Desvio padrão dos retornos diários usando PriceHistory

**Edge Cases:**
- Sem histórico de preços → volatility = null
- Divisão por zero
- Preço atual = 0

---

#### 2. Risk Analysis
**`GET /api/portfolios/{id}/risk-analysis`**

Analise risco e diversificação:

```json
{
  "overallRisk": "Medium",
  "sharpeRatio": 1.25,
  "concentrationRisk": {
    "largestPosition": {
      "symbol": "PETR4",
      "percentage": 25.5
    },
    "top3Concentration": 60.2
  },
  "sectorDiversification": [
    {
      "sector": "Energy",
      "percentage": 35.0,
      "risk": "High"
    }
  ],
  "recommendations": [
    "Reduzir exposição ao setor Energy (35%)",
    "Posição PETR4 representa 25.5% do portfólio (ideal < 20%)"
  ]
}
```

**Algoritmos Necessários:**
- **Sharpe Ratio**: `(RetornoPortfolio - TaxaSelic) / Volatilidade`
- **Concentration Risk**: 
  - Maior posição individual
  - Top 3 posições somadas
- **Sector Diversification**: Agrupar por setor e calcular %

**Regras de Risco:**
- Alto: posição > 25% OU setor > 40%
- Médio: posição 15-25% OU setor 25-40%
- Baixo: posição < 15% E setor < 25%

---

#### 3. Rebalancing Suggestions
**`GET /api/portfolios/{id}/rebalancing`**

Sugira transações para ajustar o portfólio:

```json
{
  "needsRebalancing": true,
  "currentAllocation": [
    {
      "symbol": "PETR4",
      "currentWeight": 25.5,
      "targetWeight": 20.0,
      "deviation": 5.5
    }
  ],
  "suggestedTrades": [
    {
      "symbol": "PETR4",
      "action": "SELL",
      "quantity": 50,
      "estimatedValue": 1775.00,
      "transactionCost": 5.33,
      "reason": "Reduzir de 25.5% para 20.0%"
    },
    {
      "symbol": "ITUB4",
      "action": "BUY",
      "quantity": 60,
      "estimatedValue": 1740.00,
      "transactionCost": 5.22,
      "reason": "Aumentar de 8.5% para 12.0%"
    }
  ],
  "totalTransactionCost": 10.55,
  "expectedImprovement": "Redução de 15% no risco de concentração"
}
```

**Algoritmos Necessários:**
- Calcular peso atual: `ValorPosição / ValorTotal * 100`
- Identificar desvios: `|PesoAtual - PesoAlvo| > 2%`
- Calcular quantidade a transacionar para atingir target
- Custo de transação: `0.3%` por operação
- **Otimização**: Minimizar número de trades e custos

**Regras:**
- Só sugerir se desvio > 2%
- Não sugerir trades < R$ 100,00
- Priorizar maiores desvios
- Considerar custos vs benefícios

---

### 🏗️ Estrutura Técnica Esperada

#### Arquitetura Mínima

```
├── Controllers/
│   └── AnalyticsController.cs      # 3 endpoints
├── Services/
│   ├── PerformanceCalculator.cs    # Algoritmos de performance
│   ├── RiskAnalyzer.cs             # Análise de risco
│   └── RebalancingOptimizer.cs     # Otimização de rebalanceamento
├── Models/
│   ├── Portfolio.cs, Asset.cs      # Entidades
│   └── DTOs/                       # Response models
├── Data/
│   ├── DataContext.cs              # InMemory DB
│   └── SeedData.json               # Dados fornecidos
└── Tests/
    └── ServicesTests/              # Testes unitários
```

#### O Que NÃO Precisa Implementar

❌ CRUD de Assets e Portfolios (dados já estão no seed)  
❌ Autenticação/Autorização  
❌ Banco de dados persistente  
❌ Atualização de preços  
❌ Sistema de alertas  

---

### 🚀 Como Entregar

#### 1. Submissão
- Repositório Git (GitHub/GitLab) público ou privado com acesso
- Branch `main` com código final

#### 2. Estrutura Obrigatória
```
📁 Projeto/
├── README.md                    # Instruções de execução
├── SeedData.json               # Dados fornecidos (não modificar)
├── Controllers/                # AnalyticsController
├── Services/                   # Lógica dos algoritmos
├── Models/                     # Entidades e DTOs
├── Tests/                      # Mínimo 5 testes unitários
└── Program.cs                  # Seed automático
```

#### 3. README do Projeto
Deve conter:
- Como executar (`dotnet run`)
- Como testar (`dotnet test`)
- Decisões técnicas importantes
- Fórmulas financeiras utilizadas
- Premissas adotadas

#### 4. Testes Obrigatórios (Mínimo 5)
- ✅ Cálculo de retorno total
- ✅ Cálculo de volatilidade com dados históricos
- ✅ Sharpe ratio com diferentes cenários
- ✅ Identificação de concentração de risco
- ✅ Sugestão de rebalanceamento

---

### ✅ Checklist de Entrega

#### Obrigatório
- [ ] AnalyticsController com 3 endpoints funcionais
- [ ] Services com algoritmos financeiros implementados
- [ ] Carregamento automático do SeedData.json
- [ ] 5 testes unitários passando
- [ ] Tratamento de edge cases (divisão por zero, dados faltantes)
- [ ] Documentação básica no README
- [ ] Código compilando e rodando com `dotnet run`

#### Diferencial
- [ ] Swagger/OpenAPI configurado
- [ ] Logs estruturados para debug dos cálculos
- [ ] Validação robusta de inputs
- [ ] Testes de integração
- [ ] Algoritmo de otimização de rebalanceamento avançado
- [ ] Comentários explicando fórmulas financeiras

---

### 💡 Critérios de Avaliação

| Critério | Peso | O Que Avaliamos |
|----------|------|-----------------|
| **Algoritmos** | 40% | Correção dos cálculos, tratamento de edge cases |
| **Qualidade de Código** | 30% | Clean Code, SOLID, organização |
| **Testes** | 20% | Cobertura, cenários testados |
| **Documentação** | 10% | Clareza, decisões técnicas |

---

### 🎓 Dicas

1. **Comece pelo Performance**: É o mais direto
2. **Use classes helpers**: `FinancialCalculator`, `MathHelper`
3. **Isole a lógica**: Services devem ser testáveis sem controllers
4. **Valide os dados**: O seed pode ter inconsistências propositais
5. **Documente fórmulas**: Explique cada cálculo financeiro
6. **Teste com os 3 portfólios**: Cada um tem características diferentes

---

### 📊 Dados de Teste

Use os 3 portfólios do SeedData.json:

1. **Portfólio Conservador** (user-001)
   - Foco em dividendos
   - Baixa volatilidade esperada
   - Boa diversificação

2. **Portfólio Crescimento** (user-002)
   - Ações de tecnologia e varejo
   - Alta volatilidade
   - Concentrado em poucos setores

3. **Portfólio Dividendos** (user-003)
   - Empresas maduras
   - Médio risco
   - Precisa rebalanceamento

---

### ❓ FAQ

**P: Posso usar bibliotecas externas para cálculos?**  
R: Sim, mas dê preferência a implementar os algoritmos (é o que avaliaremos).

**P: E se não houver histórico de preços?**  
R: Retorne `null` para volatilidade e documente a decisão.

**P: Preciso validar se o portfólio existe?**  
R: Sim, retorne 404 se não existir.

**P: O que fazer se TargetAllocation não somar 100%?**  
R: Documente sua decisão (normalizar, rejeitar ou aceitar).

---

**Boa sorte! 🚀**
