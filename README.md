# FinFlow — Internal Expense Reimbursement Platform (Backend)

Nền tảng quản lý **chi phí nội bộ và hoàn tiền** dạng **multi-tenant SaaS** dành cho doanh nghiệp. Nhân viên tự ứng trước chi phí công tác, nộp chứng từ (hóa đơn, biên lai) lên hệ thống → Manager phê duyệt → Accountant xử lý hoàn tiền vào tài khoản ngân hàng cá nhân.
Đây là repository **Backend** được xây dựng theo kiến trúc **Clean Architecture** (4 layers), sử dụng **CQRS/MediatR** và **GraphQL** (HotChocolate) làm API layer chính.

> 🔗 **Frontend Repository:** [FinFlow-FrontEnd](https://github.com/hort3nsia/FinFlow-FrontEnd)

---

## ✨ Tính năng chính

### 📄 Nộp & Quản lý chứng từ hoàn tiền (Document Lifecycle)
- Nhân viên upload biên lai/hóa đơn đã ứng trước (thủ công hoặc quét qua **OCR**)
- OCR tự động trích xuất thông tin từ ảnh chụp hóa đơn (Groq, OpenRouter, PaddleOCR) với **failover**
- Luồng hoàn tiền: Nhân viên nộp chứng từ → Manager review & phê duyệt → Accountant hoàn tiền
- Phát hiện chứng từ trùng lặp (chống gian lận) bằng **pgvector** embeddings
- Hỗ trợ đa loại tiền tệ với tỷ giá thời gian thực (Frankfurter API)

### 💰 Ngân sách & Tài chính (Budget & Finance)
- Mô hình ngân sách **3 trạng thái**: Allocated → Committed → Spent
- **Budget Reservation** pipeline đảm bảo tính toàn vẹn tài chính
- Concurrency control bằng **xmin tokens** (Optimistic Concurrency)
- Hỗ trợ đa tiền tệ, chuyển đổi tỷ giá tự động
- Xuất CSV ngân hàng (VCB, BIDV, TCB, Generic)

### ✅ Phê duyệt & Hoàn tiền (Approval & Reimbursement)
- Multi-level approval routing theo cấu hình tenant
- Hỗ trợ escalation flow (chuyển cấp phê duyệt cao hơn)
- Luồng: Manager phê duyệt chứng từ → Accountant xử lý hoàn tiền cho nhân viên
- Nhân viên đăng ký **hồ sơ hoàn tiền** (thông tin ngân hàng cá nhân, mã hóa AES-GCM)
- Xuất file CSV chuyển khoản theo định dạng ngân hàng VN (VCB, BIDV, TCB)
- **Audit logging** cho mọi hành động quan trọng

### 🤖 AI Chatbot (RAG-based)
- Chatbot hỗ trợ nhân viên truy vấn trạng thái hoàn tiền, chứng từ, ngân sách
- **Hybrid Retrieval**: Vector search (pgvector) + Full-text search (PostgreSQL)
- Intent classification cascade: Embedding → LLM fallback
- Streaming responses qua **GraphQL Subscriptions**
- Rate limiting, output filtering, content moderation

### 🏢 Multi-Tenancy & Membership
- Kiến trúc **multi-tenant** với data isolation theo tenant
- Hệ thống vai trò: **SuperAdmin**, **TenantAdmin**, **Manager**, **Accountant**, **Staff**
- Invitation flow: Mời thành viên qua email
- Workspace switching giữa nhiều tổ chức

### 🔐 Bảo mật & Xác thực
- **JWT** Access Token + Refresh Token (Redis cache)
- OTP xác thực email (Single Use, TTL giới hạn)
- Login rate limiting bằng Redis (chống brute-force)
- Mã hóa mật khẩu bằng **BCrypt**
- PII Encryption (AES-GCM) cho dữ liệu nhạy cảm (thông tin ngân hàng)

### 📊 Báo cáo & Analytics
- Tổng chi hoàn tiền theo phòng ban, nhân viên
- Budget utilization (ngân sách đã dùng / còn lại)
- Top employees theo số tiền hoàn
- Monthly trend analysis
- Hàng đợi hoàn tiền (Payment queue)

### 🔔 Thông báo & Subscription
- In-app notifications cho approval, payment, document events
- Subscription plans với quota enforcement
- Usage tracking per tenant và per member

---

## 🏗️ Kiến trúc

Dự án tuân thủ **Clean Architecture** (Onion Architecture):

```
FinFlow/
├── src/
│   ├── Domain/                    # Entities, Enums, Interfaces (Repository Contracts)
│   │   ├── Accounts/              # User accounts
│   │   ├── Budgets/               # Budget aggregates
│   │   ├── Departments/           # Department tree
│   │   ├── Documents/             # Reviewed & Draft documents
│   │   ├── Expenses/              # Expense tracking
│   │   ├── Vendors/               # Vendor management
│   │   ├── Chat/                  # Chat domain
│   │   ├── TenantMemberships/     # Multi-tenant membership
│   │   ├── TenantSubscriptions/   # Subscription plans
│   │   └── ...
│   │
│   ├── Application/               # Use Cases, CQRS Handlers, DTOs
│   │   ├── Auth/                  # Login, Register, OTP, Refresh
│   │   ├── Budgets/               # Budget commands & queries
│   │   ├── Chat/                  # RAG chatbot services
│   │   ├── Documents/             # OCR, Draft, Review handlers
│   │   ├── Expenses/              # Expense CRUD
│   │   ├── Payments/              # Payment processing
│   │   ├── Reporting/             # Analytics services
│   │   ├── Membership/            # Workspace, Invitation
│   │   └── ...
│   │
│   ├── Infrastructure/            # EF Core, Repositories, External Services
│   │   ├── Data/                  # DbContext, Migrations, Configurations
│   │   ├── Auth/                  # JWT, BCrypt, Rate Limiter, OTP
│   │   ├── Caching/               # Redis cache service
│   │   ├── Chat/                  # Embedding, Vector store, Intent classifiers
│   │   ├── Ocr/                   # Groq, OpenRouter, PaddleOCR providers
│   │   ├── Middleware/            # Tenant, Timeout, Idempotency
│   │   ├── Audit/                 # Audit logging middleware
│   │   └── ...
│   │
│   └── Api/                       # GraphQL API Layer
│       ├── GraphQL/               # Queries, Mutations, Subscriptions (19 modules)
│       ├── Endpoints/             # Minimal API endpoints
│       ├── Observability/         # Health checks, Serilog
│       └── Program.cs
│
├── tests/                         # Integration & Unit tests
├── docker-compose.yml             # PostgreSQL + Redis
└── FinFlow.sln
```

### Design Patterns sử dụng
- **CQRS + MediatR** — Tách command và query, mỗi handler là một class độc lập
- **Repository + Unit of Work** — Quản lý transaction và đảm bảo tính toàn vẹn dữ liệu
- **DataLoader Pattern** — Chống N+1 query trong GraphQL resolvers
- **Middleware Pipeline** — Tenant resolution, request timeout, idempotency, audit
- **Provider Pattern** — OCR multi-provider với failover chain
- **Cascade Classifier** — Intent classification: Embedding → LLM → Rule-based fallback

---

## 🛠️ Tech Stack

| Layer | Công nghệ |
|-------|-----------|
| Runtime | .NET 8, ASP.NET Core |
| API | GraphQL (HotChocolate), Minimal APIs |
| ORM | Entity Framework Core |
| Database | PostgreSQL 16, pgvector |
| Caching | Redis 7 |
| Auth | JWT, BCrypt, OTP (Email) |
| OCR | Groq Vision, OpenRouter, PaddleOCR |
| AI/Embeddings | OpenRouter Embeddings, pgvector |
| Logging | Serilog |
| Container | Docker, Docker Compose |

---

## 📋 Yêu cầu hệ thống

- .NET SDK 8.0+
- Docker Desktop
- (Tùy chọn) PostgreSQL 16+ và Redis 7+ nếu chạy không qua Docker

---

## ▶️ Cài đặt & Chạy

### 1. Clone repository

```bash
git clone https://github.com/hort3nsia/FinFlow-BackEnd.git
cd FinFlow-BackEnd
```

### 2. Cấu hình environment

```bash
cp .env.example .env
# Chỉnh sửa .env với thông tin database và API keys
```

Các biến cần thiết:
```env
POSTGRES_DB=finflow_db
POSTGRES_USER=postgres
POSTGRES_PASSWORD=your_secure_password_here
POSTGRES_PORT=5434

# OCR Providers (tùy chọn)
GROQ_API_KEY=your_groq_api_key_here
OPENROUTER_API_KEY=your_openrouter_api_key_here
```

### 3. Khởi động Database & Redis

```bash
docker-compose up -d
```

### 4. Chạy API

```bash
cd src/Api
dotnet run
```

Truy cập:
- **GraphQL Playground:** http://localhost:5219/graphql
- **Swagger UI:** http://localhost:5219/swagger (Development mode)
- **Health Check:** http://localhost:5219/health

---

## 📡 GraphQL API Modules

| Module | Queries | Mutations | Mô tả |
|--------|---------|-----------|--------|
| Auth | ✅ | ✅ | Login, Register, OTP, Refresh Token |
| Documents | ✅ | ✅ | OCR scan, draft, review, submit |
| Payments | ✅ | ✅ | Payment processing, refunds |
| Budgets | ✅ | ✅ | Budget CRUD, reservation |
| Departments | ✅ | ✅ | Department tree management |
| Membership | ✅ | ✅ | Invite, roles, workspace |
| Vendors | ✅ | ✅ | Vendor management |
| Chat | ✅ | ✅ | RAG chatbot (+ Subscriptions) |
| Categories | ✅ | — | Expense categories |
| Subscriptions | ✅ | ✅ | Plan management, quota |
| ExchangeRates | ✅ | ✅ | Currency conversion |
| Employees | ✅ | ✅ | Reimbursement profiles |
| Bank Export | ✅ | ✅ | CSV export (VCB/BIDV/TCB) |
| Reporting | ✅ | — | Analytics & dashboards |
| Notifications | ✅ | ✅ | In-app notifications |
| TenantSettings | ✅ | ✅ | Branding, policies |
| Platform (Admin) | ✅ | ✅ | SuperAdmin console |

---

## 🧪 CLI Tools

```bash
# Rebuild document chunk indexes
dotnet run -- reindex-chunks <tenantId>

# Re-embed all document chunks
dotnet run -- reembed-chunks [batchSize]

# Evaluate intent classification accuracy
dotnet run -- eval-intents [casesDir] [outFile]

# Evaluate RAG retrieval quality
dotnet run -- eval-rag [goldenPath] [outFile]
```
