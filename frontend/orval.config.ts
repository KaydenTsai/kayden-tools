import { defineConfig } from 'orval';

export default defineConfig({
    // SnapSplit 分帳工具 API
    snapSplit: {
        input: {
            target: 'http://localhost:5063/swagger/snapsplit/swagger.json',
        },
        output: {
            mode: 'tags-split',
            target: './src/api/endpoints',
            schemas: './src/api/models',
            client: 'react-query',
            override: {
                mutator: {
                    path: './src/shared/lib/axios-instance.ts',
                    name: 'customInstance',
                },
                query: {
                    useQuery: true,
                    useMutation: true,
                    signal: true,
                },
            },
        },
        hooks: {
            afterAllFilesWrite: 'prettier --write',
        },
    },

    // URL Shortener 短網址服務 API
    urlShortener: {
        input: {
            target: 'http://localhost:5063/swagger/urlshortener/swagger.json',
        },
        output: {
            mode: 'tags-split',
            target: './src/api/endpoints',
            schemas: './src/api/models',
            client: 'react-query',
            override: {
                mutator: {
                    path: './src/shared/lib/axios-instance.ts',
                    name: 'customInstance',
                },
                query: {
                    useQuery: true,
                    useMutation: true,
                    signal: true,
                },
            },
        },
        hooks: {
            afterAllFilesWrite: 'prettier --write',
        },
    },

    // Common API（Auth, Health）
    common: {
        input: {
            target: 'http://localhost:5063/swagger/common/swagger.json',
        },
        output: {
            mode: 'tags-split',
            target: './src/api/endpoints',
            schemas: './src/api/models',
            client: 'react-query',
            override: {
                mutator: {
                    path: './src/shared/lib/axios-instance.ts',
                    name: 'customInstance',
                },
                query: {
                    useQuery: true,
                    useMutation: true,
                    signal: true,
                },
            },
        },
        hooks: {
            afterAllFilesWrite: 'prettier --write',
        },
    },

    // Picky Diet 減肥應用 API（未來擴充）
    // pickyDiet: {
    //     input: {
    //         target: 'http://localhost:5063/swagger/pickydiet/swagger.json',
    //     },
    //     output: {
    //         mode: 'tags-split',
    //         target: './src/api/endpoints',
    //         schemas: './src/api/models',
    //         client: 'react-query',
    //         override: {
    //             mutator: {
    //                 path: './src/shared/lib/axios-instance.ts',
    //                 name: 'customInstance',
    //             },
    //             query: {
    //                 useQuery: true,
    //                 useMutation: true,
    //                 signal: true,
    //             },
    //         },
    //     },
    //     hooks: {
    //         afterAllFilesWrite: 'prettier --write',
    //     },
    // },
});
