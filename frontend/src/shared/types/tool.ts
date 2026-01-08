export interface Tool {
    id: string;
    name: string;
    description: string;
    path: string;
    icon: string;
    category: ToolCategory;
    tags: string[];
}

export type ToolCategory = 'dev' | 'daily';

export const categoryLabels: Record<ToolCategory, string> = {
    dev: '開發者工具',
    daily: '日常工具',
};
