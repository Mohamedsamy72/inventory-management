# 20 — Deployment & Infrastructure Architecture

> **Document ID:** SPEC-20  
> **Topic:** Production Topology, Containerization, Reverse Proxy, HTTPS, and Secrets Management

---

## 1. Production Topology

```
                  ┌─────────────────────────────────────┐
                  │          Cloudflare / CDN           │ (WAF, SSL Termination, DDoS Protection)
                  └──────────────────┬──────────────────┘
                                     │ (HTTPS)
         ┌───────────────────────────┴───────────────────────────┐
         │                                                       │
         ▼                                                       ▼
┌─────────────────────────────────┐             ┌─────────────────────────────────┐
│     Next.js Frontend Node       │             │     ASP.NET Core Web API        │
│    (Vercel / Docker Container)  │             │   (Kestrel / Docker Container)  │
└─────────────────────────────────┘             └────────────────┬────────────────┘
                                                                 │ (Encrypted Wire)
                                                                 ▼
                                                ┌─────────────────────────────────┐
                                                │      Managed PostgreSQL 16+     │
                                                │   (Automated Backups & WAL)     │
                                                └─────────────────────────────────┘
```

---

## 2. Infrastructure Security Baseline

1. **Secrets Management:** Database connection strings, encryption keys, and SMS credentials are fed via environment variables or secret vaults (e.g., Azure Key Vault / AWS Secrets Manager). **Zero plaintext secrets in git repositories.**
2. **Reverse Proxy Configuration:** Nginx or Caddy reverse proxies enforce:
   - Modern TLS (TLS 1.3).
   - Strict Content Security Policy (CSP), HSTS, and X-Frame-Options: DENY.
3. **Database Backups:** Continuous Write-Ahead Logging (WAL) archiving with point-in-time recovery (PITR) up to 30 days.
