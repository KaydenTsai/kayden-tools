import { defineConfig } from 'orval';

export default defineConfig({
  api: {
    input: {
      // Use live endpoint when backend is running, or static file
      target: process.env.SWAGGER_URL || 'http://localhost:5000/swagger/v1/swagger.json',
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
