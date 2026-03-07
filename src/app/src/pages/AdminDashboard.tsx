import { useState, useEffect, useCallback } from 'react';
import {
  Card,
  CardHeader,
  Text,
  Button,
  Spinner,
  Divider,
  Badge,
  tokens,
  makeStyles,
} from '@fluentui/react-components';
import { getAdminMetrics, getAdminInsights, type AdminMetrics } from '../api/commitApi';

const useStyles = makeStyles({
  page: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingVerticalL,
    maxWidth: '900px',
    margin: '0 auto',
  },
  pageHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  kpiGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(4, 1fr)',
    gap: tokens.spacingHorizontalM,
  },
  kpiCard: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
    padding: tokens.spacingVerticalM,
    textAlign: 'center',
  },
  kpiValue: {
    fontSize: '32px',
    fontWeight: tokens.fontWeightBold,
    color: tokens.colorBrandForeground1,
    lineHeight: '1.1',
  },
  kpiLabel: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
  insightPanel: {
    padding: tokens.spacingVerticalM,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  insightText: {
    whiteSpace: 'pre-wrap',
    lineHeight: '1.6',
    color: tokens.colorNeutralForeground1,
    fontSize: tokens.fontSizeBase300,
  },
  metricsNote: {
    padding: tokens.spacingVerticalS,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
    marginTop: tokens.spacingVerticalXS,
  },
});

interface AdminDashboardProps {
  authToken?: string;
}

export function AdminDashboard({ authToken }: AdminDashboardProps): JSX.Element {
  const styles = useStyles();
  const [metrics, setMetrics] = useState<AdminMetrics | null>(null);
  const [insights, setInsights] = useState<string | null>(null);
  const [loadingMetrics, setLoadingMetrics] = useState(false);
  const [loadingInsights, setLoadingInsights] = useState(false);
  const [lastRefreshed, setLastRefreshed] = useState<Date | null>(null);

  const loadMetrics = useCallback(async () => {
    setLoadingMetrics(true);
    try {
      const data = await getAdminMetrics(authToken);
      setMetrics(data);
      setLastRefreshed(new Date());
    } catch {
      setMetrics(null);
    } finally {
      setLoadingMetrics(false);
    }
  }, [authToken]);

  const loadInsights = useCallback(async () => {
    setLoadingInsights(true);
    try {
      const data = await getAdminInsights(authToken);
      setInsights(data.insights);
    } catch {
      setInsights('Failed to generate insights. Check Azure OpenAI configuration.');
    } finally {
      setLoadingInsights(false);
    }
  }, [authToken]);

  useEffect(() => { void loadMetrics(); }, [loadMetrics]);

  const kpis = [
    {
      label: 'Total Commitments',
      value: metrics ? metrics.totalCommitments.toLocaleString() : '—',
      emoji: '📋',
    },
    {
      label: 'Feedback Events',
      value: metrics ? metrics.totalFeedback.toLocaleString() : '—',
      emoji: '💬',
    },
    {
      label: 'Avg Confidence',
      value: metrics ? metrics.avgConfidence.toFixed(2) : '—',
      emoji: '🎯',
    },
    {
      label: 'False Positive Rate',
      value: metrics ? `${(metrics.falsePositiveRate * 100).toFixed(0)}%` : '—',
      emoji: metrics?.falsePositiveRate != null && metrics.falsePositiveRate > 0.3 ? '⚠️' : '✅',
    },
  ];

  return (
    <div className={styles.page}>
      {/* Header */}
      <div className={styles.pageHeader}>
        <div>
          <Text size={600} weight="bold">Commit FHL Admin</Text>
          {lastRefreshed && (
            <Text size={100} style={{ display: 'block', color: tokens.colorNeutralForeground3 }}>
              Last refreshed: {lastRefreshed.toLocaleTimeString()}
            </Text>
          )}
        </div>
        <Button
          appearance="secondary"
          disabled={loadingMetrics}
          onClick={loadMetrics}
          icon={loadingMetrics ? <Spinner size="tiny" /> : undefined}
        >
          {loadingMetrics ? 'Refreshing...' : '↻ Refresh'}
        </Button>
      </div>

      {/* KPI Cards */}
      <div className={styles.kpiGrid}>
        {kpis.map(kpi => (
          <Card key={kpi.label}>
            <div className={styles.kpiCard}>
              <Text className={styles.kpiValue}>{kpi.emoji} {kpi.value}</Text>
              <Text className={styles.kpiLabel}>{kpi.label}</Text>
            </div>
          </Card>
        ))}
      </div>

      {/* AI Insights */}
      <Card>
        <CardHeader
          header={
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', width: '100%' }}>
              <Text weight="semibold" size={400}>✨ AI Insights</Text>
              <Button
                appearance="subtle"
                size="small"
                disabled={loadingInsights}
                onClick={loadInsights}
                icon={loadingInsights ? <Spinner size="tiny" /> : undefined}
              >
                {loadingInsights ? 'Generating...' : insights ? 'Regenerate' : 'Generate Insights'}
              </Button>
            </div>
          }
        />
        <div className={styles.insightPanel}>
          {loadingInsights ? (
            <div style={{ display: 'flex', gap: tokens.spacingHorizontalS, alignItems: 'center' }}>
              <Spinner size="small" />
              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                Generating AI insights...
              </Text>
            </div>
          ) : insights ? (
            <Text className={styles.insightText}>{insights}</Text>
          ) : (
            <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
              Click "Generate Insights" to get an AI-powered analysis of your extraction performance.
            </Text>
          )}
        </div>
      </Card>

      {/* Metrics Note */}
      <Card>
        <CardHeader header={<Text weight="semibold" size={400}>📊 Metrics</Text>} />
        <div className={styles.insightPanel}>
          <div className={styles.metricsNote}>
            <Text size={200} style={{ color: tokens.colorNeutralForeground2 }}>
              <strong>Note:</strong> Detailed time-series metrics, per-source confidence histograms,
              and feedback distribution charts require Azure Monitor / Application Insights KQL queries.
              Connect your Application Insights workspace to enable full analytics.
            </Text>
          </div>
          <Divider />
          <div style={{ display: 'flex', gap: tokens.spacingHorizontalL, flexWrap: 'wrap' }}>
            <div>
              <Text size={200} weight="semibold">Extraction Sources</Text>
              <div style={{ marginTop: tokens.spacingVerticalXS }}>
                {['Transcript', 'Chat', 'Email', 'ADO', 'Drive', 'Planner'].map(s => (
                  <Badge key={s} appearance="tint" style={{ marginRight: '4px', marginBottom: '4px' }}>
                    {s}
                  </Badge>
                ))}
              </div>
            </div>
            <div>
              <Text size={200} weight="semibold">API Endpoints</Text>
              <div style={{ marginTop: tokens.spacingVerticalXS, display: 'flex', flexDirection: 'column', gap: '4px' }}>
                <Text size={100} style={{ fontFamily: 'monospace', color: tokens.colorNeutralForeground2 }}>GET /api/v1/admin/metrics</Text>
                <Text size={100} style={{ fontFamily: 'monospace', color: tokens.colorNeutralForeground2 }}>GET /api/v1/admin/insights</Text>
                <Text size={100} style={{ fontFamily: 'monospace', color: tokens.colorNeutralForeground2 }}>POST /api/v1/commitments/{'{id}'}/feedback</Text>
                <Text size={100} style={{ fontFamily: 'monospace', color: tokens.colorNeutralForeground2 }}>POST /api/v1/extract/preview</Text>
                <Text size={100} style={{ fontFamily: 'monospace', color: tokens.colorNeutralForeground2 }}>POST /api/v1/missed-extraction</Text>
              </div>
            </div>
          </div>
        </div>
      </Card>
    </div>
  );
}
