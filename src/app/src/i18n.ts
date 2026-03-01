import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import enTranslation from './locales/en/translation.json';
import enPsychology from './locales/en/psychology.json';

export function initI18n(locale: string = 'en'): void {
  i18n.use(initReactI18next).init({
    resources: {
      en: {
        translation: enTranslation,
        psychology: enPsychology,
      },
    },
    lng: locale,
    fallbackLng: 'en',
    interpolation: { escapeValue: false },
    defaultNS: 'translation',
  });
}

export { i18n };
