/** @type {import('jest').Config} */
module.exports = {
  testEnvironment: 'jsdom',
  preset: 'ts-jest',
  extensionsToTreatAsEsm: ['.ts', '.tsx'],
  moduleNameMapper: {
    // Strip .js extension from imports (ESM compat)
    '^(\\.{1,2}/.*)\\.js$': '$1',
    // Map import.meta.env to a stub
    '^virtual:.*$': '<rootDir>/src/__tests__/__mocks__/virtual.ts',
    // Stub api.config — uses import.meta.env which ts-jest cannot parse
    '.*[/\\\\]config[/\\\\]api\\.config$': '<rootDir>/src/__tests__/__mocks__/api.config.ts',
  },
  transform: {
    '^.+\\.[tj]sx?$': [
      'ts-jest',
      {
        useESM: true,
        tsconfig: {
          jsx: 'react-jsx',
          esModuleInterop: true,
          allowSyntheticDefaultImports: true,
        },
      },
    ],
  },
  // Transform ESM-only packages that jest can't load natively
  transformIgnorePatterns: [
    'node_modules/(?!(' +
      '@react-spring|' +
      '@fluentui|' +
      '@tanstack|' +
      'i18next|' +
      'react-i18next|' +
      '@microsoft/teams-js' +
    ')/)',
  ],
  setupFilesAfterEnv: ['<rootDir>/src/__tests__/setup.ts'],
  // Suppress import.meta errors — handled via globals below
  globals: {
    'import.meta': { env: { VITE_API_BASE_URL: 'https://test-api.example.com' } },
  },
  testMatch: ['<rootDir>/src/__tests__/**/*.test.{ts,tsx}'],
  collectCoverageFrom: [
    'src/**/*.{ts,tsx}',
    '!src/main.tsx',
    '!src/i18n.ts',
    '!src/**/*.d.ts',
  ],
};
