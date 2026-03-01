import type { Config } from 'jest';

/**
 * Jest configuration — frontend TypeScript only.
 * The C# backend uses xUnit (src/api/CommitApi.Tests/).
 * Jest covers: src/app/src/ components + hooks + utils, and tests/unit/ factories.
 */
const config: Config = {
  rootDir: '..',
  projects: [
    {
      displayName: 'app',
      preset: 'ts-jest',
      testEnvironment: 'jsdom',
      rootDir: '.',
      testMatch: [
        '<rootDir>/src/app/src/**/*.test.{ts,tsx}',
        '<rootDir>/tests/unit/**/*.test.ts',
      ],
      moduleNameMapper: {
        '^@app/(.*)$': '<rootDir>/src/app/src/$1',
        '^@types/(.*)$': '<rootDir>/src/app/src/types/$1',
        // Fluent UI and CSS modules
        '\\.(css|less|scss)$': '<rootDir>/tests/__mocks__/styleMock.ts',
      },
      transform: {
        '^.+\\.tsx?$': ['ts-jest', {
          tsconfig: '<rootDir>/src/app/tsconfig.json',
          diagnostics: { ignoreCodes: ['TS151001'] },
        }],
      },
      setupFilesAfterFramework: ['@testing-library/jest-dom'],
    },
  ],
  collectCoverageFrom: [
    '<rootDir>/src/app/src/**/*.{ts,tsx}',
    '!<rootDir>/src/app/src/**/*.d.ts',
    '!<rootDir>/src/app/src/main.tsx',
    '!<rootDir>/src/app/src/i18n.ts',
    '!<rootDir>/src/app/src/locales/**',
  ],
  coverageThreshold: {
    global: { lines: 90, branches: 85 },
  },
};

export default config;
