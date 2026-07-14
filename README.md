# 🏦 Teste Prático: Sistema de Analytics de Portfólio

## 1. Implementação e uso

API .NET 8 para analisar performance, risco e rebalanceamento de carteiras pré-carregadas. Os dados são somente leitura e vêm de `Data/SeedData.json`; não há CRUD, autenticação ou banco persistente nesta entrega.

### Pré-requisitos

- .NET SDK 8.0 ou superior compatível com `net8.0`.
- O modo CP-SAT usa o runtime nativo do Google OR-Tools; no Windows, o Microsoft Visual C++ Redistributable 2022 x64 deve estar disponível.
- Execute os comandos abaixo a partir da raiz do repositório.

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
| `GET /api/portfolios/{id}/rebalancing?mode={mode}` | Plano recomendado, métricas objetivas e comparação opcional entre estratégias. |

Os IDs das três carteiras seed são `1`, `2` e `3`. IDs não positivos ou modos inválidos retornam `400`, carteiras inexistentes retornam `404`, dados incompletos retornam `422` com `ProblemDetails`, e excesso de requisições retorna `429`.

O rebalanceamento aceita `recommended`, `exhaustive`, `quadraticProgramming`, `cpSat` e `compareAll` (padrão). Como é um `GET` somente leitura, chamadas repetidas com a mesma carteira e modo são idempotentes; não é necessário `Idempotency-Key`.

Exemplo de chamada, depois de substituir `{url}` pelo endereço informado no console:

```powershell
Invoke-RestMethod "{url}/api/portfolios/1/performance"
Invoke-RestMethod "{url}/api/portfolios/1/risk-analysis"
Invoke-RestMethod "{url}/api/portfolios/1/rebalancing"
Invoke-RestMethod "{url}/api/portfolios/1/rebalancing?mode=cpSat"
```

### Arquitetura e responsabilidades

- `Portfolio-Financeiro-WebApplication`: controller HTTP, Swagger e composição da aplicação.
- `Application`: casos de uso e algoritmos puros de performance, risco e rebalanceamento.
- `Models`: agregados `Portfolio` e `Asset`, posições e regras de negócio.
- `DAL`: EF Core InMemory, repositórios, queries e semeadura do JSON.
- `IoC`: registro de dependências.
- `tests`: testes unitários, de persistência/integração e de API.

O cálculo de rebalanceamento usa Strategy + Registry/Factory + Orchestrator. `ExhaustiveSubsetOptimizationStrategy` compara subconjuntos, `QuadraticProgrammingOptimizationStrategy` minimiza erro quadrático e turnover, e `CpSatOptimizationStrategy` usa o solver inteiro Google OR-Tools. Todos os planos passam pelo mesmo avaliador de tracking error, custo, benefício líquido e autofinanciamento antes da seleção.

#### Decisão deliberada: por que três algoritmos de rebalanceamento?

Optei deliberadamente por implementar as três estratégias porque o enunciado apresenta a otimização avançada de rebalanceamento como diferencial, e identifiquei uma oportunidade concreta de substituir uma heurística isolada por uma solução mais sofisticada e, ao mesmo tempo, aplicar Strategy + Registry/Factory + Orchestrator em um problema no qual os algoritmos são realmente intercambiáveis. Não se trata de uma expansão lateral do escopo: as três implementações atendem ao mesmo caso de uso, compartilham contrato, regras, métricas e validações, e aprofundam diretamente o requisito de otimização proposto.

Financeiramente, nenhuma técnica domina as demais em todos os portfólios. A busca exaustiva oferece maior explicabilidade em universos pequenos ao comparar todos os subconjuntos elegíveis; a programação quadrática produz uma solução contínua que equilibra aderência ao alvo e turnover; e o CP-SAT representa valores monetários e decisões de ativação de trades de forma discreta, incorporando valor mínimo, custos e autofinanciamento. Disponibilizar `exhaustive`, `quadraticProgramming`, `cpSat` e a comparação `compareAll` permite ao usuário avaliar explicitamente o compromisso entre tracking error, custo, quantidade de operações e benefício líquido, reduz a dependência de um único modelo e torna auditável a escolha da recomendação final.

### Configuração do banco InMemory

Há um único arquivo de configuração: [appsettings.json](rsc/Portfolio-Financeiro-WebApplication/appsettings.json). Para o provider EF Core InMemory não existe connection string; o identificador equivalente é o nome do banco.

- `Database:InMemory:ProductionName`: `portfolio-analytics-production`, usado pela API nos ambientes normais.
- `Database:InMemory:IntegrationTestName`: `portfolio-analytics-integration-tests`, usado pela API quando o ambiente é `Testing`.
- `RateLimiting:Analytics:PermitLimit`: máximo de requisições por janela e IP (`60`).
- `RateLimiting:Analytics:WindowSeconds`: duração da janela fixa (`60` segundos).

Os projetos `Api.Tests` e `Persistence.Tests` copiam esse mesmo arquivo ao build. A fábrica dos testes de API força o ambiente `Testing` e lê a cópia do arquivo, portanto não usa o nome de produção. Os testes de persistência ainda acrescentam um identificador único por caso ao store InMemory, isolando execuções paralelas.

### Fórmulas financeiras e premissas

- **Retorno total:** `((valor atual - valor investido) / valor investido) × 100`. `totalInvestment` é o capital inicial de `Portfolio.TotalInvestment`, mesmo quando a soma das posições é diferente.
- **Retorno anualizado:** `((1 + retorno total / 100)^(365 / dias decorridos) - 1) × 100`.
- **Peso da posição:** `(valor de mercado da posição / valor de mercado total) × 100`.
- **Retorno diário:** `(fechamento[t] - fechamento[t-1]) / fechamento[t-1]`.
- **Volatilidade de performance:** desvio-padrão populacional dos retornos diários ponderados; é retornada em base diária. Quando apenas parte das posições possui histórico utilizável, seus valores de mercado são renormalizados para que os pesos cobertos somem 100%.
- **Volatilidade usada no Sharpe:** a mesma série diária coberta, anualizada por `√252`.
- **Sharpe Ratio:** `(retorno anualizado - Selic anual) / volatilidade anualizada`. O retorno anualizado usa `Portfolio.TotalInvestment`, a mesma base do endpoint de performance, e não é reconstruído pela soma dos custos das posições.
- **Custo de transação:** `valor negociado × 0,3%`, arredondado comercialmente para centavos.
- **Rebalanceamento:** o valor-alvo é `valor pós-custos × peso-alvo`; o plano resolve `valor pós-custos + custos = valor atual`, para que vendas líquidas financiem compras e taxas. A quantidade é `valor negociado / preço atual`.
- **Tracking error:** `Σ |peso projetado - peso alvo|`.
- **Benefício líquido:** `tracking error antes - tracking error depois - (custo / valor do portfólio × 100)`.
- **Seleção multiobjetivo:** planos dentro de 95% do melhor benefício líquido são equivalentes; entre eles vence o de menor número de trades, depois menor custo e menor tracking error.

### Regras de negócio e edge cases

- Cada `TargetAllocation` deve estar entre 0% e 100%; carteiras com posições devem somar 100%, com tolerância de 0,0001%. Violações lançam `BusinessViolationException`.
- O otimizador normaliza targets como proteção adicional para dados externos/legados, mas a entidade `Portfolio` rejeita esse estado na criação ou alteração normal.
- Uma cotação duplicada no mesmo dia é consolidada pela cotação mais recente, que passa a ser o fechamento diário.
- Sem qualquer histórico suficiente, sem datas comuns entre os históricos disponíveis, divisão por zero, preço atual zero ou datas inválidas: a métrica que não pode ser calculada retorna `null` ou não gera trade, conforme o endpoint. Histórico parcial é calculado sobre as posições cobertas, com pesos renormalizados.
- Trades só são sugeridos para desvios maiores que 2 pontos percentuais e valores de pelo menos R$100. Operações são ordenadas pelo maior desvio e o plano evita aporte externo.
- CP-SAT trabalha em centavos inteiros, com limite determinístico de um segundo; falha ou indisponibilidade do runtime nativo é reportada na alternativa sem derrubar o endpoint.
- Uma carteira existente sem posições é tratada como dado incompleto por Performance (`422`), pois não há base para calcular retorno, mas como estado degenerado válido por Risk e Rebalancing (`200`, métricas neutras/nulas e nenhuma ação necessária). Já a ausência de um ativo ou cotação exigido por uma posição existente continua sendo dado incompleto (`422`) nos três casos.

### Observabilidade

A categoria `Application` está configurada em nível `Debug` no único `appsettings.json`. Os casos de uso registram, com `Operation` e `PortfolioId`, o carregamento dos dados, entradas relevantes, resultados dos cálculos e operações de rebalanceamento. Os algoritmos puros não recebem logger, preservando testabilidade e responsabilidade única.

### Estado da entrega

- AnalyticsController com três endpoints funcionais, Swagger com schema e exemplo completo da resposta de performance, e testes de integração.
- Seed automático via `IDataSower` na inicialização.
- Algoritmos financeiros, validações, tratamento de dados faltantes e logs estruturados.
- Três estratégias de rebalanceamento comparáveis, endpoint idempotente e rate limit por IP.
- Testes unitários e de integração para cálculos, regras de negócio, persistência e API. As três estratégias de rebalanceamento possuem cenários end-to-end com resultados determinísticos e validação de tracking error, benefício líquido, custos, valor mínimo das operações e autofinanciamento.

Na última validação local, passaram 122 testes de `SharedKernel.Tests`, 45 de `Application.Tests`, 14 de `Persistence.Tests`, 25 de `Api.Tests`, 8 de `IoC.Tests`, 4 de `Abstractions.Tests` e 1 teste de `Domain.Tests`: 219 testes na solução completa. O build completo termina com zero erros e zero warnings.

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
