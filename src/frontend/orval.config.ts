import { defineConfig } from 'orval';

export default defineConfig({
  api: {
    input: {
      target: process.env.SWAGGER_URL || 'http://localhost:5063/swagger/v1/swagger.json',
      validation: false,
    },
    output: {
      mode: 'tags-split',
      target: './api/endpoints',
      schemas: './api/models',
      client: 'react-query',
      httpClient: 'axios',
      clean: true,
      prettier: true,
      override: {
        mutator: {
          path: './api/axios-instance.ts',
          name: 'axiosInstance',
        },
        query: {
          useQuery: true,
          useMutation: true,
          signal: true,
        },
      },
    },
  },
});
