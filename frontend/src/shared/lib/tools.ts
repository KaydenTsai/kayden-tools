import type {Tool} from '@/shared/types/tool';

export const tools: Tool[] = [
    {
        id: 'json-formatter',
        name: 'JSON Formatter',
        description: '格式化、驗證、壓縮 JSON 資料',
        path: '/tools/json',
        icon: 'DataObject',
        category: 'dev',
        tags: ['json', 'format', 'validate', 'minify'],
    },
    {
        id: 'base64',
        name: 'Base64 編解碼',
        description: '文字或檔案的 Base64 編碼與解碼',
        path: '/tools/base64',
        icon: 'Code',
        category: 'dev',
        tags: ['base64', 'encode', 'decode'],
    },
    {
        id: 'jwt-decoder',
        name: 'JWT Decoder',
        description: '解析 JWT Token，顯示 Header、Payload 和過期時間',
        path: '/tools/jwt',
        icon: 'Key',
        category: 'dev',
        tags: ['jwt', 'token', 'decode', 'auth'],
    },
    {
        id: 'timestamp',
        name: '時間戳轉換',
        description: 'Unix Timestamp 與日期時間互相轉換',
        path: '/tools/timestamp',
        icon: 'Schedule',
        category: 'dev',
        tags: ['timestamp', 'unix', 'date', 'time'],
    },
    {
        id: 'uuid-generator',
        name: 'UUID 產生器',
        description: '產生 UUID v4、v7 和 ULID',
        path: '/tools/uuid',
        icon: 'Fingerprint',
        category: 'dev',
        tags: ['uuid', 'guid', 'ulid', 'generate'],
    },
    {
        id: 'snapsplit',
        name: 'Snapsplit',
        description: '快速分帳算出誰應該付給誰，用最少轉帳次數結清',
        path: '/tools/snapsplit',
        icon: 'Calculate',
        category: 'daily',
        tags: ['split', 'bill', 'payment', 'transfer', '分帳'],
    }
];

// key/value 索引，方便快速取用
export const toolsById = Object.fromEntries(
    tools.map(tool => [tool.id, tool])
) as Record<string, Tool>;

export const getToolById = (id: string): Tool | undefined => {
    return toolsById[id];
};

export const getToolsByCategory = (category: string): Tool[] => {
    return tools.filter((tool) => tool.category === category);
};

export const searchTools = (query: string): Tool[] => {
    const lowerQuery = query.toLowerCase();
    return tools.filter(
        (tool) =>
            tool.name.toLowerCase().includes(lowerQuery) ||
            tool.description.toLowerCase().includes(lowerQuery) ||
            tool.tags.some((tag) => tag.includes(lowerQuery))
    );
};
