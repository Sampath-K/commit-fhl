import { useState, useCallback } from 'react';
import {
  Dialog,
  DialogTrigger,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogContent,
  DialogActions,
  Button,
  Checkbox,
  Text,
  Spinner,
  tokens,
  makeStyles,
} from '@fluentui/react-components';
import { runRescan } from '../../api/commitApi';

const ALL_SOURCES = [
  { key: 'transcript', label: 'Meetings & Transcripts', emoji: '🎙️' },
  { key: 'chat',       label: 'Teams Chat',              emoji: '💬' },
  { key: 'email',      label: 'Email',                   emoji: '📧' },
  { key: 'ado',        label: 'Azure DevOps',            emoji: '🔧' },
  { key: 'drive',      label: 'OneDrive / SharePoint',   emoji: '📄' },
  { key: 'planner',    label: 'Planner',                 emoji: '📋' },
];

const useStyles = makeStyles({
  sliderRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    margin: `${tokens.spacingVerticalS} 0`,
  },
  sourceGrid: {
    display: 'grid',
    gridTemplateColumns: '1fr 1fr',
    gap: tokens.spacingVerticalXS,
    margin: `${tokens.spacingVerticalS} 0`,
  },
  resultMsg: {
    marginTop: tokens.spacingVerticalS,
    padding: tokens.spacingVerticalS,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorStatusSuccessBackground1,
    border: `1px solid ${tokens.colorStatusSuccessBorder1}`,
  },
});

interface RescanModalProps {
  userId: string;
  authToken?: string;
  onRescanComplete?: () => void;
}

export function RescanModal({ userId, authToken, onRescanComplete }: RescanModalProps): JSX.Element {
  const styles = useStyles();
  const [open, setOpen] = useState(false);
  const [days, setDays] = useState(7);
  const [sources, setSources] = useState<Set<string>>(new Set(ALL_SOURCES.map(s => s.key)));
  const [running, setRunning] = useState(false);
  const [result, setResult] = useState<string | null>(null);

  const toggleSource = useCallback((key: string) => {
    setSources(prev => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }, []);

  const handleRescan = useCallback(async () => {
    setRunning(true);
    setResult(null);
    try {
      await runRescan(userId, days, Array.from(sources), authToken);
      setResult(`Extraction complete — your task list has been refreshed.`);
      onRescanComplete?.();
    } catch (err) {
      setResult(`Rescan failed: ${String(err)}`);
    } finally {
      setRunning(false);
    }
  }, [userId, days, sources, authToken, onRescanComplete]);

  return (
    <Dialog open={open} onOpenChange={(_, d) => { setOpen(d.open); if (!d.open) { setResult(null); } }}>
      <DialogTrigger disableButtonEnhancement>
        <Button appearance="subtle" size="small" onClick={() => setOpen(true)}>
          🔄 Rescan
        </Button>
      </DialogTrigger>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Rescan for Tasks</DialogTitle>
          <DialogContent>
            <Text size={300}>Look back over the last N days across your communication channels.</Text>

            {/* Days slider */}
            <div className={styles.sliderRow}>
              <Text size={200} weight="semibold">Look back:</Text>
              <input
                type="range"
                min={1}
                max={30}
                value={days}
                onChange={e => setDays(Number(e.target.value))}
                style={{ flex: 1 }}
              />
              <Text size={200} weight="semibold" style={{ minWidth: '50px' }}>
                {days} day{days !== 1 ? 's' : ''}
              </Text>
            </div>

            {/* Source checkboxes */}
            <Text size={200} weight="semibold">Sources:</Text>
            <div className={styles.sourceGrid}>
              {ALL_SOURCES.map(s => (
                <Checkbox
                  key={s.key}
                  label={`${s.emoji} ${s.label}`}
                  checked={sources.has(s.key)}
                  onChange={() => toggleSource(s.key)}
                />
              ))}
            </div>

            {/* Result message */}
            {result && (
              <div className={styles.resultMsg}>
                <Text size={200}>{result}</Text>
              </div>
            )}
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={() => setOpen(false)}>Cancel</Button>
            <Button
              appearance="primary"
              disabled={running || sources.size === 0}
              onClick={handleRescan}
              icon={running ? <Spinner size="tiny" /> : undefined}
            >
              {running ? 'Scanning...' : 'Rescan'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
