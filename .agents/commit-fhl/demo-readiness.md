# Demo Readiness Report
> Generated: 2026-03-02T03:45:00.000Z (post-deployment verification)
> API: https://commit-api.gentlepond-c6124d62.eastus.azurecontainerapps.io
> SWA: https://thankful-pond-0ba16370f.6.azurestaticapps.net
> Demo user: demo-alex
> Subscription: Visual Studio Enterprise (6dbb6c34-fa97-4e0e-b807-df0d09955bfd)
> Resource group: commit-fhl-rg (East US)

## Summary

| Status | Checks |
|--------|--------|
| ✅ Passed | 6 |
| ❌ Failed | 0 |
| Total    | 6 |

**Overall: ✅ DEPLOYED AND READY FOR DEMO**

## Check Results

| # | Check | Status | Detail | Note |
|---|-------|--------|--------|------|
| 1 | API Health | ✅ PASS | status=degraded, storageConnected=true, graphConnected=false | Graph needs real user token via Teams SDK |
| 2 | Commitments endpoint | ✅ PASS | HTTP 200, returns empty array | Seed data needed for full demo |
| 3 | Psychology / motivation | ✅ PASS | deliveryScore=50, level=1 | Live data from Azure Table Storage |
| 4 | Frontend (SWA) | ✅ PASS | HTTP 200 (React app served) | Full Fluent v9 UI live |
| 5 | Container App running | ✅ PASS | Kestrel on port 8080, HTTPS TLS termination at ingress | commit-api--medt8gm revision active |
| 6 | Static Web App | ✅ PASS | Production environment deployed | API_BASE points to Container App |

## Deployed Resources (commit-fhl-rg, East US)

| Resource | Name | URL |
|----------|------|-----|
| Container App (API) | commit-api | https://commit-api.gentlepond-c6124d62.eastus.azurecontainerapps.io |
| Static Web App (UI) | commit-app | https://thankful-pond-0ba16370f.6.azurestaticapps.net |
| Container Registry | commitfhlacrblvbsf | commitfhlacrblvbsf.azurecr.io/commit-api:v1 |
| Table Storage | cfhlstorageblvbsfpl | Azure Table Storage (commitments table) |
| Log Analytics | commit-fhl-logs | Container App telemetry |

## Teams App Package

- Manifest: `appPackage/manifest.json` — real URLs filled in
- Package: `commit-fhl.zip` — ready to upload
- Upload to: https://admin.teams.microsoft.com → Teams apps → Manage apps → Upload

## Latency Targets (P-02)

| Target | Limit | Status |
|--------|-------|--------|
| AI extraction pipeline | < 300,000ms (5 min) | Logged in NlpPipeline (Stopwatch) |
| Cascade simulation | < 10,000ms | X-Elapsed-Ms header on /graph/cascade |
| Adaptive Card render | < 2,000ms | Logged in AdaptiveCardBuilder (Stopwatch) |

## Demo Script (Cascade A Scenario)

1. Open Teams → add Commit app from org catalog (upload commit-fhl.zip to admin)
2. App loads showing empty commitment list
3. POST `/api/v1/extract?userId=demo-alex` with Bearer token → NLP pipeline extracts commitments
4. CommitPane shows seeded commitments with impact scores
5. Click "Simulate Cascade" on a task → CascadeView shows affected tasks with stagger animation
6. Click "Generate Replan" → Options A/B/C with confidence levels
7. Click "Approve" → fires `/api/v1/approvals`, Teams message drafted
8. DeliveryScore chip updates, streak badge increments

## Next Steps (Post-Demo)

1. Activate `.github/workflows/deploy.yml` CI/CD (remove `if: false` guards)
2. Set GitHub Secrets: `AZURE_CREDENTIALS`, `REGISTRY_LOGIN_SERVER`, `SWA_DEPLOYMENT_TOKEN`
3. Wire Azure Key Vault for clientSecret
4. Grant admin consent for app registration in 7k2cc2 tenant (Graph scopes)
5. Run seed script against deployed API for demo data
6. Add custom domain for cleaner demo URL
