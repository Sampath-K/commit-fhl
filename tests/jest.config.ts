import type { Config } from 'jest';

const config: Config = {
  projects: [
    {
      displayName: 'api-unit',
      preset: 'ts-jest',
      testEnvironment: 'node',
      testMatch: ['<rootDir>/unit/**/*.test.ts'],
      moduleNameMapper: {
        '^@/(.*)$': '<rootDir>/../src/api/src/$1',
      },
    },
    {
      displayName: 'api-integration',
      preset: 'ts-jest',
      testEnvironment: 'node',
      testMatch: ['<rootDir>/integration/**/*.test.ts'],
      moduleNameMapper: {
        '^@/(.*)$': '<rootDir>/../src/api/src/$1',
      },
    },
  ],
  collectCoverageFrom: [
    '<rootDir>/../src/api/src/**/*.ts',
    '!<rootDir>/../src/api/src/types/**',
    '!<rootDir>/../src/api/src/index.ts',
  ],
  coverageThreshold: {
    global: { lines: 90, branches: 85 },
  },
};

export default config;
