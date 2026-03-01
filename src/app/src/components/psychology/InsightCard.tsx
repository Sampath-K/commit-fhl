import { Text, tokens, makeStyles } from '@fluentui/react-components';

// ─── Styles ───────────────────────────────────────────────────────────────────

const useStyles = makeStyles({
  card: {
    display:         'flex',
    alignItems:      'center',
    gap:             tokens.spacingHorizontalM,
    padding:         `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalM}`,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius:    tokens.borderRadiusMedium,
  },
  icon: {
    fontSize:   tokens.fontSizeBase500,
    flexShrink: '0',
  },
  body: {
    display:       'flex',
    flexDirection: 'column',
  },
});

// ─── Component ────────────────────────────────────────────────────────────────

interface InsightCardProps {
  icon:     string;
  headline: string;
  detail?:  string;
}

export function InsightCard({ icon, headline, detail }: InsightCardProps): JSX.Element {
  const styles = useStyles();
  return (
    <div className={styles.card}>
      <span className={styles.icon} role="img" aria-hidden="true">{icon}</span>
      <div className={styles.body}>
        <Text size={200} weight="semibold">{headline}</Text>
        {detail && (
          <Text size={100} style={{ color: tokens.colorNeutralForeground3 }}>{detail}</Text>
        )}
      </div>
    </div>
  );
}

export default InsightCard;
