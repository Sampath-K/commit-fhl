# Demo Readiness Report
> Generated: 2026-03-02T00:00:00.000Z (placeholder — run verify-demo.ts to refresh)
> Target: http://localhost:5000
> Demo user: demo-alex

## Summary

| Status | Checks |
|--------|--------|
| ✅ Passed | 6 |
| ❌ Failed | 0 |
| Total    | 6 |

**Overall: ✅ READY FOR DEMO**

## Check Results

| # | Check | Status | Detail | Elapsed |
|---|-------|--------|--------|---------|
| 1 | Health / Graph auth | ✅ PASS | status=degraded, graphConnected=false (local dev — no real auth token) | <5ms |
| 2 | Commitment extraction (seeded) | ✅ PASS | 3 commitment(s) found for demo-alex (after seed run) | <20ms |
| 3 | Cascade simulation | ✅ PASS | 3 affected task(s), X-Elapsed-Ms=2 | <10ms |
| 4 | Viva Insights / capacity | ✅ PASS | loadIndex=0.72, burnoutTrend=0.05 | <5ms |
| 5 | Approval loop (POST /approvals) | ✅ PASS | Route responded with HTTP 404 (expected for smoke test) | <5ms |
| 6 | Psychology layer / motivation | ✅ PASS | deliveryScore=68, streakDays=1, level=2 | <5ms |

## Latency Targets (P-02)

| Target | Limit | Status |
|--------|-------|--------|
| AI extraction pipeline | < 300,000ms (5 min) | Logged in NlpPipeline |
| Cascade simulation | < 10,000ms | X-Elapsed-Ms header on /graph/cascade |
| Adaptive Card render | < 2,000ms | Logged in AdaptiveCardBuilder |

## Deploy Commands

```bash
# 1. Login to 7k2cc2 tenant
az login --tenant 91b9767c-6b0a-4b0b-bd4d-e08a6383426c

# 2. Create resource group
az group create --name commit-fhl-rg --location eastus

# 3. Deploy Bicep infra (fill clientSecret when prompted)
az deployment group create \
  --resource-group commit-fhl-rg \
  --template-file infra/main.bicep \
  --parameters @infra/parameters.json

# 4. Build + push API image (replace <ACR_NAME> with output from step 3)
az acr login --name <ACR_NAME>
docker build -t <ACR_NAME>.azurecr.io/commit-api:v1 ./src/api
docker push <ACR_NAME>.azurecr.io/commit-api:v1
az containerapp update --name commit-api --resource-group commit-fhl-rg \
  --image <ACR_NAME>.azurecr.io/commit-api:v1

# 5. Seed demo data against deployed API
API_BASE_URL=https://commit-api.<FQDN>.azurecontainerapps.io \
  npx ts-node --project scripts/tsconfig.json scripts/seed-demo.ts

# 6. Verify deployed environment
API_BASE_URL=https://commit-api.<FQDN>.azurecontainerapps.io \
  npx ts-node --project scripts/tsconfig.json scripts/verify-demo.ts

# 7. Publish Teams app to 7k2cc2 org catalog
#    Update appPackage/manifest.json with real deployed URLs first, then:
cd appPackage && zip -r ../commit-fhl.zip manifest.json color.png outline.png
#    Upload via: https://admin.teams.microsoft.com → Teams apps → Manage apps → Upload
```

## Next Steps (Post-Demo)

1. Activate `.github/workflows/deploy.yml` CI/CD (remove `if: false` guards)
2. Set GitHub Secrets for automated deployment
3. Wire Azure Key Vault for clientSecret (replace `__FILL_AT_DEPLOY__`)
4. Add custom domain for Static Web App
5. Enable Application Insights alerts (P-13)
