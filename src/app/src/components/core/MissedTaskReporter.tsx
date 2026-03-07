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
  Textarea,
  Select,
  Text,
  Spinner,
  tokens,
  makeStyles,
} from '@fluentui/react-components';
import { reportMissedExtraction } from '../../api/commitApi';

const SOURCE_TYPES = [
  { value: 'chat',       label: '💬 Teams Chat' },
  { value: 'email',      label: '📧 Email' },
  { value: 'transcript', label: '🎙️ Meeting Transcript' },
  { value: 'other',      label: '📌 Other' },
];

const useStyles = makeStyles({
  field: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
    marginBottom: tokens.spacingVerticalM,
  },
  result: {
    marginTop: tokens.spacingVerticalS,
    padding: tokens.spacingVerticalS,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorStatusSuccessBackground1,
    border: `1px solid ${tokens.colorStatusSuccessBorder1}`,
  },
});

interface MissedTaskReporterProps {
  userId: string;
  authToken?: string;
  onTaskAdded?: () => void;
}

export function MissedTaskReporter({ userId, authToken, onTaskAdded }: MissedTaskReporterProps): JSX.Element {
  const styles = useStyles();
  const [open, setOpen] = useState(false);
  const [text, setText] = useState('');
  const [sourceType, setSourceType] = useState('chat');
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState<{ found: boolean; title?: string } | null>(null);

  const handleSubmit = useCallback(async () => {
    if (!text.trim()) return;
    setSubmitting(true);
    setResult(null);
    try {
      const res = await reportMissedExtraction(userId, text.trim(), sourceType, authToken);
      setResult({ found: res.found, title: res.taskTitle });
      if (res.found) onTaskAdded?.();
    } catch {
      setResult({ found: false });
    } finally {
      setSubmitting(false);
    }
  }, [userId, text, sourceType, authToken, onTaskAdded]);

  const handleClose = useCallback(() => {
    setOpen(false);
    setText('');
    setResult(null);
  }, []);

  return (
    <Dialog open={open} onOpenChange={(_, d) => { if (!d.open) handleClose(); else setOpen(true); }}>
      <DialogTrigger disableButtonEnhancement>
        <Button appearance="transparent" size="small" onClick={() => setOpen(true)}
          style={{ color: tokens.colorNeutralForeground3, fontSize: tokens.fontSizeBase200 }}>
          Report a missed task
        </Button>
      </DialogTrigger>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Report a Missed Task</DialogTitle>
          <DialogContent>
            <Text size={300}>
              Paste text where a commitment was missed. We'll use this to improve extraction.
            </Text>

            <div className={styles.field} style={{ marginTop: tokens.spacingVerticalM }}>
              <Text size={200} weight="semibold">Source type</Text>
              <Select
                value={sourceType}
                onChange={(_, d) => setSourceType(d.value)}
              >
                {SOURCE_TYPES.map(s => (
                  <option key={s.value} value={s.value}>{s.label}</option>
                ))}
              </Select>
            </div>

            <div className={styles.field}>
              <Text size={200} weight="semibold">Text containing the missed task</Text>
              <Textarea
                value={text}
                onChange={(_, d) => setText(d.value.slice(0, 2000))}
                placeholder="Paste the message or email snippet here..."
                resize="vertical"
                rows={6}
              />
              <Text size={100} style={{ color: tokens.colorNeutralForeground3 }}>
                {text.length}/2000 characters
              </Text>
            </div>

            {result !== null && (
              <div className={styles.result}>
                {result.found
                  ? <Text size={200}>✅ Found task: "{result.title}" — added to your list!</Text>
                  : <Text size={200}>Thank you! We'll use this to improve extraction.</Text>
                }
              </div>
            )}
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={handleClose}>Cancel</Button>
            <Button
              appearance="primary"
              disabled={submitting || !text.trim()}
              onClick={handleSubmit}
              icon={submitting ? <Spinner size="tiny" /> : undefined}
            >
              {submitting ? 'Submitting...' : 'Report'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
