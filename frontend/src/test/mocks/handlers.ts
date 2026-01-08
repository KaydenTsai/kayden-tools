import {http, HttpResponse} from 'msw';

const API_URL = 'http://localhost:5063/api';

export const handlers = [
    // Auth handlers
    http.post(`${API_URL}/Auth/login`, async ({request}) => {
        const body = await request.json() as {email?: string};
        if (body.email === 'test@example.com') {
            return HttpResponse.json({
                success: true,
                data: {
                    accessToken: 'mock-access-token',
                    refreshToken: 'mock-refresh-token',
                    expiresAt: new Date(Date.now() + 3600000).toISOString(),
                    user: {id: '1', email: 'test@example.com', name: 'Test User'},
                },
            });
        }
        return HttpResponse.json(
            {status: 401, title: 'Unauthorized', detail: 'Invalid credentials'},
            {status: 401}
        );
    }),

    http.post(`${API_URL}/Auth/refresh`, () => {
        return HttpResponse.json({
            success: true,
            data: {
                accessToken: 'new-mock-access-token',
                refreshToken: 'new-mock-refresh-token',
                expiresAt: new Date(Date.now() + 3600000).toISOString(),
            },
        });
    }),

    // Bills handlers (SnapSplit)
    http.get(`${API_URL}/Bills`, () => {
        return HttpResponse.json([
            {id: '1', name: '聚餐費用', createdAt: new Date().toISOString()},
            {id: '2', name: '旅遊支出', createdAt: new Date().toISOString()},
        ]);
    }),

    http.get(`${API_URL}/Bills/:id`, ({params}) => {
        if (params.id === '999') {
            return HttpResponse.json(
                {status: 404, title: 'Not Found', errorCode: 'RESOURCE_NOT_FOUND'},
                {status: 404}
            );
        }
        return HttpResponse.json({
            id: params.id,
            name: '測試帳單',
            members: [],
            expenses: [],
            createdAt: new Date().toISOString(),
        });
    }),

    http.post(`${API_URL}/Bills`, async ({request}) => {
        const body = await request.json() as Record<string, unknown>;
        return HttpResponse.json(
            {id: 'new-bill-id', ...body, createdAt: new Date().toISOString()},
            {status: 201}
        );
    }),

    // Generic fallback for unhandled requests
    http.all('*', ({request}) => {
        console.warn(`Unhandled ${request.method} request to ${request.url}`);
        return HttpResponse.json(
            {status: 404, title: 'Not Found'},
            {status: 404}
        );
    }),
];
