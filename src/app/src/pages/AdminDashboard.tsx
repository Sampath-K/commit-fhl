import { useState, useEffect, useCallback } from 'react';
import {
  Card, CardHeader, Text, Button, Spinner, Divider, Badge,
  tokens, makeStyles, Tab, TabList,
} from '@fluentui/react-components';
import {
  getAdminMetrics, getAdminInsights, getAdminFeedback, getAdminSignalProfiles,
  type AdminMetrics, type FeedbackListResult, type SignalProfilesResult,
} from '../api/commitApi';

const useStyles = makeStyles({
  page: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingVerticalL,
    maxWidth: '960px',
    margin: '0 auto',
  },
  pageHeader: { display: 'flex', justifyContent: 'space-between', alignItems: 'center' },
  kpiGrid: { display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: tokens.spacingHorizontalM },
  kpiCard: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS, padding: tokens.spacingVerticalM, textAlign: 'center' },
  kpiValue: { fontSize: '32px', fontWeight: tokens.fontWeightBold, color: tokens.colorBrandForeground1, lineHeight: '1.1' },
  kpiLabel: { fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 },
  insightPanel: { padding: tokens.spacingVerticalM, display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS },
  insightText: { whiteSpace: 'pre-wrap', lineHeight: '1.6', color: tokens.colorNeutralForeground1, fontSize: tokens.fontSizeBase300 },
  // Feedback tab
  filterBar: { display: 'flex', gap: tokens.spacingHorizontalM, alignItems: 'center', flexWrap: 'wrap', padding: `${tokens.spacingVerticalS} 0` },
  table: { width: '100%', borderCollapse: 'collapse' as const },
  th: { textAlign: 'left' as const, fontSize: tokens.fontSizeBase200, fontWeight: tokens.fontWeightSemibold, color: tokens.colorNeutralForeground3, padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalM}`, borderBottom: `1px solid ${tokens.colorNeutralStroke2}` },
  td: { fontSize: tokens.fontSizeBase200, padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalM}`, borderBottom: `1px solid ${tokens.colorNeutralBackground3}`, verticalAlign: 'middle' as const },
  breakdownGrid: { display: 'grid', gridTemplateColumns: '1fr 1fr', gap: tokens.spacingHorizontalM },
  barRow: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS, marginBottom: '4px' },
  bar: { height: '10px', borderRadius: '5px', backgroundColor: tokens.colorBrandBackground, minWidth: '4px' },
  // Pipeline tab
  pipelineIntro: { padding: tokens.spacingVerticalM, backgroundColor: tokens.colorNeutralBackground2, borderRadius: tokens.borderRadiusMedium, border: `1px solid ${tokens.colorNeutralStroke2}` },
  stepFlow: { display: 'flex', gap: tokens.spacingHorizontalS, alignItems: 'center', flexWrap: 'wrap', padding: `${tokens.spacingVerticalS} 0` },
  step: { padding: `4px 10px`, borderRadius: tokens.borderRadiusCircular, backgroundColor: tokens.colorNeutralBackground3, fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground2, border: `1px solid ${tokens.colorNeutralStroke2}` },
  arrow: { color: tokens.colorNeutralForeground3, fontSize: tokens.fontSizeBase200 },
  userTable: { width: '100%', borderCollapse: 'collapse' as const },
  aggGrid: { display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: tokens.spacingHorizontalM },
});

const TYPE_COLORS: Record<string, string> = {
  Confirm:       '#107C10',
  FalsePositive: '#D83B01',
  WrongOwner:    '#8764B8',
  Duplicate:     '#0078D4',
};

const SOURCE_EMOJIS: Record<string, string> = {
  Transcript: '📹', Chat: '💬', Email: '📧', Ado: '🔧', Drive: '📄', Planner: '📋',
};

function FpRateBadge({ rate }: { rate: number }): JSX.Element {
  const pct = Math.round(rate * 100);
  const color = rate > 0.6 ? '#D83B01' : rate > 0.3 ? '#FF8C00' : '#107C10';
  return <span style={{ fontWeight: 600, color, fontSize: '12px' }}>{pct}%</span>;
}

function ConfAdjBadge({ adj }: { adj: number }): JSX.Element {
  if (adj === 0) return <span style={{ color: '#666', fontSize: '12px' }}>none</span>;
  const label = adj >= 0.15 ? `+${adj} (high FP)` : `+${adj} (moderate FP)`;
  return <span style={{ fontWeight: 600, color: '#D83B01', fontSize: '12px' }}>{label}</span>;
}

interface AdminDashboardProps { authToken?: string; }

export function AdminDashboard({ authToken }: AdminDashboardProps): JSX.Element {
  const styles = useStyles();
  const [tab, setTab] = useState<'overview' | 'feedback' | 'pipeline'>('overview');

  // ── Overview state ────────────────────────────────────────────────────────
  const [metrics, setMetrics]           = useState<AdminMetrics | null>(null);
  const [insights, setInsights]         = useState<string | null>(null);
  const [loadingMetrics, setLM]         = useState(false);
  const [loadingInsights, setLI]        = useState(false);
  const [lastRefreshed, setLastRefreshed] = useState<Date | null>(null);

  // ── Feedback tab state ────────────────────────────────────────────────────
  const [feedbackData, setFeedbackData]  = useState<FeedbackListResult | null>(null);
  const [fbLoading, setFbLoading]        = useState(false);
  const [typeFilter, setTypeFilter]      = useState('');
  const [sourceFilter, setSourceFilter]  = useState('');

  // ── Pipeline tab state ────────────────────────────────────────────────────
  const [profiles, setProfiles]    = useState<SignalProfilesResult | null>(null);
  const [profLoading, setProfLoading] = useState(false);

  const loadMetrics = useCallback(async () => {
    setLM(true);
    try { const d = await getAdminMetrics(authToken); setMetrics(d); setLastRefreshed(new Date()); }
    catch { setMetrics(null); }
    finally { setLM(false); }
  }, [authToken]);

  const loadInsights = useCallback(async () => {
    setLI(true);
    try { const d = await getAdminInsights(authToken); setInsights(d.insights); }
    catch { setInsights('Failed to generate insights. Check Azure OpenAI configuration.'); }
    finally { setLI(false); }
  }, [authToken]);

  const loadFeedback = useCallback(async () => {
    setFbLoading(true);
    try { const d = await getAdminFeedback(typeFilter || undefined, sourceFilter || undefined, 200, authToken); setFeedbackData(d); }
    catch { setFeedbackData(null); }
    finally { setFbLoading(false); }
  }, [authToken, typeFilter, sourceFilter]);

  const loadProfiles = useCallback(async () => {
    setProfLoading(true);
    try { const d = await getAdminSignalProfiles(authToken); setProfiles(d); }
    catch { setProfiles(null); }
    finally { setProfLoading(false); }
  }, [authToken]);

  useEffect(() => { void loadMetrics(); }, [loadMetrics]);
  useEffect(() => { if (tab === 'feedback')  void loadFeedback();  }, [tab, loadFeedback]);
  useEffect(() => { if (tab === 'pipeline')  void loadProfiles();  }, [tab, loadProfiles]);

  // ─── KPI cards ─────────────────────────────────────────────────────────────
  const kpis = [
    { label: 'Total Commitments', value: metrics ? metrics.totalCommitments.toLocaleString() : '—', emoji: '📋' },
    { label: 'Feedback Events',   value: metrics ? metrics.totalFeedback.toLocaleString()    : '—', emoji: '💬' },
    { label: 'Avg Confidence',    value: metrics ? metrics.avgConfidence.toFixed(2)           : '—', emoji: '🎯' },
    {
      label: 'False Positive Rate',
      value: metrics ? `${(metrics.falsePositiveRate * 100).toFixed(0)}%` : '—',
      emoji: metrics?.falsePositiveRate != null && metrics.falsePositiveRate > 0.3 ? '⚠️' : '✅',
    },
  ];

  const maxBreakdownVal = (obj: Record<string, number>) =>
    Math.max(1, ...Object.values(obj));

  return (
    <div className={styles.page}>
      {/* ── Page header ─────────────────────────────────────────────────────── */}
      <div className={styles.pageHeader}>
        <div>
          <Text size={600} weight="bold">Commit FHL Admin</Text>
          {lastRefreshed && tab === 'overview' && (
            <Text size={100} style={{ display: 'block', color: tokens.colorNeutralForeground3 }}>
              Last refreshed: {lastRefreshed.toLocaleTimeString()}
            </Text>
          )}
        </div>
        {tab === 'overview' && (
          <Button appearance="secondary" disabled={loadingMetrics} onClick={loadMetrics}
            icon={loadingMetrics ? <Spinner size="tiny" /> : undefined}>
            {loadingMetrics ? 'Refreshing…' : '↻ Refresh'}
          </Button>
        )}
        {tab === 'feedback' && (
          <Button appearance="secondary" disabled={fbLoading} onClick={loadFeedback}
            icon={fbLoading ? <Spinner size="tiny" /> : undefined}>
            {fbLoading ? 'Loading…' : '↻ Refresh'}
          </Button>
        )}
        {tab === 'pipeline' && (
          <Button appearance="secondary" disabled={profLoading} onClick={loadProfiles}
            icon={profLoading ? <Spinner size="tiny" /> : undefined}>
            {profLoading ? 'Loading…' : '↻ Refresh'}
          </Button>
        )}
      </div>

      {/* ── Tabs ────────────────────────────────────────────────────────────── */}
      <TabList selectedValue={tab} onTabSelect={(_, d) => setTab(d.value as typeof tab)}>
        <Tab value="overview">Overview</Tab>
        <Tab value="feedback">Feedback <Badge size="small" appearance="filled" color="informative" style={{ marginLeft: '4px' }}>{metrics?.totalFeedback ?? '…'}</Badge></Tab>
        <Tab value="pipeline">Pipeline Health</Tab>
      </TabList>

      {/* ════════════════════════════════════════════════════════════════════ */}
      {/* OVERVIEW TAB                                                       */}
      {/* ════════════════════════════════════════════════════════════════════ */}
      {tab === 'overview' && (
        <>
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

          <Card>
            <CardHeader header={
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', width: '100%' }}>
                <Text weight="semibold" size={400}>✨ AI Insights</Text>
                <Button appearance="subtle" size="small" disabled={loadingInsights} onClick={loadInsights}
                  icon={loadingInsights ? <Spinner size="tiny" /> : undefined}>
                  {loadingInsights ? 'Generating…' : insights ? 'Regenerate' : 'Generate Insights'}
                </Button>
              </div>
            } />
            <div className={styles.insightPanel}>
              {loadingInsights
                ? <div style={{ display: 'flex', gap: tokens.spacingHorizontalS, alignItems: 'center' }}><Spinner size="small" /><Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>Generating…</Text></div>
                : insights
                ? <Text className={styles.insightText}>{insights}</Text>
                : <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>Click "Generate Insights" to get an AI-powered analysis.</Text>
              }
            </div>
          </Card>
        </>
      )}

      {/* ════════════════════════════════════════════════════════════════════ */}
      {/* FEEDBACK TAB                                                       */}
      {/* ════════════════════════════════════════════════════════════════════ */}
      {tab === 'feedback' && (
        <>
          {/* Filter bar */}
          <Card>
            <div className={styles.filterBar}>
              <Text size={200} weight="semibold">Filter:</Text>
              <select
                value={typeFilter}
                onChange={e => setTypeFilter(e.target.value)}
                style={{ fontSize: '13px', padding: '4px 8px', borderRadius: '4px', border: `1px solid ${tokens.colorNeutralStroke2}` }}
              >
                <option value="">All types</option>
                <option value="Confirm">👍 Confirm</option>
                <option value="FalsePositive">👎 False Positive</option>
                <option value="WrongOwner">👤 Wrong Owner</option>
                <option value="Duplicate">🔁 Duplicate</option>
              </select>
              <select
                value={sourceFilter}
                onChange={e => setSourceFilter(e.target.value)}
                style={{ fontSize: '13px', padding: '4px 8px', borderRadius: '4px', border: `1px solid ${tokens.colorNeutralStroke2}` }}
              >
                <option value="">All sources</option>
                {['Transcript', 'Chat', 'Email', 'Ado', 'Drive', 'Planner'].map(s => (
                  <option key={s} value={s}>{SOURCE_EMOJIS[s]} {s}</option>
                ))}
              </select>
              <Button size="small" appearance="primary" onClick={loadFeedback} disabled={fbLoading}>
                {fbLoading ? 'Loading…' : 'Apply'}
              </Button>
              {feedbackData && (
                <Text size={200} style={{ color: tokens.colorNeutralForeground3, marginLeft: 'auto' }}>
                  {feedbackData.total} event{feedbackData.total !== 1 ? 's' : ''}
                </Text>
              )}
            </div>
          </Card>

          {/* Breakdown cards */}
          {feedbackData && (
            <div className={styles.breakdownGrid}>
              <Card>
                <CardHeader header={<Text weight="semibold" size={300}>By Type</Text>} />
                <div style={{ padding: `0 ${tokens.spacingHorizontalM} ${tokens.spacingVerticalM}` }}>
                  {Object.entries(feedbackData.breakdown.byType).sort((a, b) => b[1] - a[1]).map(([type, count]) => (
                    <div key={type} className={styles.barRow}>
                      <Text size={200} style={{ width: '110px', flexShrink: 0 }}>
                        <span style={{ display: 'inline-block', width: '10px', height: '10px', borderRadius: '50%', backgroundColor: TYPE_COLORS[type] ?? '#888', marginRight: '6px', verticalAlign: 'middle' }} />
                        {type}
                      </Text>
                      <div className={styles.bar} style={{ width: `${Math.round((count / maxBreakdownVal(feedbackData.breakdown.byType)) * 120)}px` }} />
                      <Text size={100} style={{ color: tokens.colorNeutralForeground3 }}>{count}</Text>
                    </div>
                  ))}
                  {Object.keys(feedbackData.breakdown.byType).length === 0 && (
                    <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>No data</Text>
                  )}
                </div>
              </Card>
              <Card>
                <CardHeader header={<Text weight="semibold" size={300}>By Source</Text>} />
                <div style={{ padding: `0 ${tokens.spacingHorizontalM} ${tokens.spacingVerticalM}` }}>
                  {Object.entries(feedbackData.breakdown.bySource).sort((a, b) => b[1] - a[1]).map(([src, count]) => (
                    <div key={src} className={styles.barRow}>
                      <Text size={200} style={{ width: '110px', flexShrink: 0 }}>{SOURCE_EMOJIS[src] ?? '📌'} {src}</Text>
                      <div className={styles.bar} style={{ width: `${Math.round((count / maxBreakdownVal(feedbackData.breakdown.bySource)) * 120)}px` }} />
                      <Text size={100} style={{ color: tokens.colorNeutralForeground3 }}>{count}</Text>
                    </div>
                  ))}
                  {Object.keys(feedbackData.breakdown.bySource).length === 0 && (
                    <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>No data</Text>
                  )}
                </div>
              </Card>
            </div>
          )}

          {/* Feedback events table */}
          <Card>
            <CardHeader header={<Text weight="semibold" size={400}>Recent Events</Text>} />
            <div style={{ overflowX: 'auto' as const, padding: `0 ${tokens.spacingHorizontalM} ${tokens.spacingVerticalM}` }}>
              {fbLoading ? (
                <div style={{ display: 'flex', gap: tokens.spacingHorizontalS, alignItems: 'center', padding: tokens.spacingVerticalM }}>
                  <Spinner size="small" /><Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>Loading feedback…</Text>
                </div>
              ) : feedbackData && feedbackData.items.length > 0 ? (
                <table className={styles.table}>
                  <thead>
                    <tr>
                      <th className={styles.th}>When</th>
                      <th className={styles.th}>Type</th>
                      <th className={styles.th}>Source</th>
                      <th className={styles.th}>Task ID</th>
                      <th className={styles.th}>Confidence</th>
                      <th className={styles.th}>Comment</th>
                    </tr>
                  </thead>
                  <tbody>
                    {feedbackData.items.map((item, i) => (
                      <tr key={i}>
                        <td className={styles.td}>
                          <Text size={200}>{new Date(item.recordedAt).toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })}</Text>
                        </td>
                        <td className={styles.td}>
                          <span style={{
                            display: 'inline-block', padding: '2px 8px', borderRadius: '12px',
                            backgroundColor: (TYPE_COLORS[item.type] ?? '#888') + '22',
                            color: TYPE_COLORS[item.type] ?? '#888',
                            fontSize: '11px', fontWeight: 600,
                          }}>{item.type === 'FalsePositive' ? '👎 FalsePositive' : item.type === 'Confirm' ? '👍 Confirm' : item.type}</span>
                        </td>
                        <td className={styles.td}>
                          <Text size={200}>{SOURCE_EMOJIS[item.sourceType] ?? '📌'} {item.sourceType}</Text>
                        </td>
                        <td className={styles.td}>
                          <Text size={100} style={{ fontFamily: 'monospace', color: tokens.colorNeutralForeground3 }}>{item.idRef}…</Text>
                        </td>
                        <td className={styles.td}>
                          <Text size={200}>{item.confidence > 0 ? item.confidence.toFixed(2) : '—'}</Text>
                        </td>
                        <td className={styles.td}>
                          <Text size={200} style={{ color: item.comment ? tokens.colorNeutralForeground1 : tokens.colorNeutralForeground3 }}>
                            {item.comment ?? '—'}
                          </Text>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : !fbLoading ? (
                <div style={{ padding: tokens.spacingVerticalM, textAlign: 'center' }}>
                  <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                    No feedback events yet. Users submit feedback via 👍/👎 on commitment cards.
                  </Text>
                </div>
              ) : null}
            </div>
          </Card>
        </>
      )}

      {/* ════════════════════════════════════════════════════════════════════ */}
      {/* PIPELINE TAB                                                       */}
      {/* ════════════════════════════════════════════════════════════════════ */}
      {tab === 'pipeline' && (
        <>
          {/* How the loop works */}
          <Card>
            <CardHeader header={<Text weight="semibold" size={400}>🔄 How Feedback Improves Extraction</Text>} />
            <div className={styles.pipelineIntro}>
              <div className={styles.stepFlow}>
                {[
                  '👎 User clicks FalsePositive',
                  '→',
                  'FeedbackEntity stored (PII-scrubbed)',
                  '→',
                  'SignalProfileService cache invalidated',
                  '→',
                  'Next extraction loads updated profile',
                  '→',
                  '3 pipeline effects applied',
                ].map((s, i) => s === '→'
                  ? <span key={i} className={styles.arrow}>{s}</span>
                  : <span key={i} className={styles.step}>{s}</span>
                )}
              </div>
              <Divider style={{ margin: `${tokens.spacingVerticalS} 0` }} />
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: tokens.spacingHorizontalM }}>
                {[
                  { icon: '📈', title: 'Confidence Threshold', desc: 'FP rate > 30% → +0.05 confidence penalty. FP rate > 60% → +0.15. Harder to extract low-confidence items for this user.' },
                  { icon: '🚫', title: 'Fingerprint Suppression', desc: 'Each 👎 hashes the task title. Identical/similar titles are silently suppressed from all future extraction runs for this user.' },
                  { icon: '🧠', title: 'NLP Context Injection', desc: 'FP source types become negative examples in the LLM extraction prompt. Confirmed sources become positive examples. Prompt adapts per-user.' },
                ].map(({ icon, title, desc }) => (
                  <div key={title} style={{ padding: tokens.spacingVerticalS, backgroundColor: tokens.colorNeutralBackground1, borderRadius: tokens.borderRadiusMedium, border: `1px solid ${tokens.colorNeutralStroke2}` }}>
                    <Text size={400}>{icon}</Text>
                    <Text size={200} weight="semibold" style={{ display: 'block', marginTop: '4px' }}>{title}</Text>
                    <Text size={100} style={{ color: tokens.colorNeutralForeground3, marginTop: '4px', display: 'block', lineHeight: '1.5' }}>{desc}</Text>
                  </div>
                ))}
              </div>
            </div>
          </Card>

          {/* Aggregate signal stats */}
          {profiles && (
            <div className={styles.aggGrid}>
              {[
                { label: 'Users with feedback', value: String(profiles.aggregate.userCount), emoji: '👤' },
                { label: 'Avg FP Rate', value: `${(profiles.aggregate.avgFpRate * 100).toFixed(0)}%`, emoji: profiles.aggregate.avgFpRate > 0.3 ? '⚠️' : '✅' },
                { label: 'Avg Conf. Adj.', value: profiles.aggregate.avgConfidenceAdjustment > 0 ? `+${profiles.aggregate.avgConfidenceAdjustment.toFixed(2)}` : 'none', emoji: '🎯' },
                { label: 'Total Suppressed', value: String(profiles.aggregate.totalSuppressed), emoji: '🚫' },
              ].map(kpi => (
                <Card key={kpi.label}>
                  <div className={styles.kpiCard}>
                    <Text className={styles.kpiValue}>{kpi.emoji} {kpi.value}</Text>
                    <Text className={styles.kpiLabel}>{kpi.label}</Text>
                  </div>
                </Card>
              ))}
            </div>
          )}

          {/* Per-user signal profile table */}
          <Card>
            <CardHeader header={<Text weight="semibold" size={400}>Per-User Signal Profiles (anonymized)</Text>} />
            <div style={{ overflowX: 'auto' as const, padding: `0 ${tokens.spacingHorizontalM} ${tokens.spacingVerticalM}` }}>
              {profLoading ? (
                <div style={{ display: 'flex', gap: tokens.spacingHorizontalS, alignItems: 'center', padding: tokens.spacingVerticalM }}>
                  <Spinner size="small" /><Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>Loading profiles…</Text>
                </div>
              ) : profiles && profiles.users.length > 0 ? (
                <table className={styles.userTable}>
                  <thead>
                    <tr>
                      <th className={styles.th}>User (hash prefix)</th>
                      <th className={styles.th}>Feedback events</th>
                      <th className={styles.th}>FP rate</th>
                      <th className={styles.th}>Conf. adjustment</th>
                      <th className={styles.th}>Suppressed titles</th>
                      <th className={styles.th}>Last feedback</th>
                    </tr>
                  </thead>
                  <tbody>
                    {profiles.users.map((u, i) => (
                      <tr key={i}>
                        <td className={styles.td}><Text size={100} style={{ fontFamily: 'monospace' }}>{u.userRef}…</Text></td>
                        <td className={styles.td}><Text size={200}>{u.totalFeedback}</Text></td>
                        <td className={styles.td}><FpRateBadge rate={u.fpRate} /></td>
                        <td className={styles.td}><ConfAdjBadge adj={u.confidenceAdjustment} /></td>
                        <td className={styles.td}>
                          {u.suppressedCount > 0
                            ? <span style={{ display: 'inline-block', padding: '2px 8px', borderRadius: '12px', backgroundColor: '#D83B0122', color: '#D83B01', fontSize: '11px', fontWeight: 600 }}>🚫 {u.suppressedCount}</span>
                            : <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>0</Text>
                          }
                        </td>
                        <td className={styles.td}>
                          <Text size={200}>{new Date(u.lastFeedbackAt).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}</Text>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : !profLoading ? (
                <div style={{ padding: tokens.spacingVerticalM, textAlign: 'center' }}>
                  <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                    No signal profiles yet. Profile data appears here after users submit feedback.
                  </Text>
                </div>
              ) : null}
            </div>
          </Card>
        </>
      )}
    </div>
  );
}
