import {useState, useMemo} from 'react';
import {Link} from 'react-router-dom';
import {Binary, Clock, Code, FileJson, Hash, Key, LayoutGrid, Receipt, Search, Wallet} from 'lucide-react';
import {Card, CardDescription, CardHeader, CardTitle} from '@/shared/components/ui/card';
import {Input} from '@/shared/components/ui/input';
import {cn} from '@/shared/lib/utils';
import {useSearchInput} from '@/shared/hooks/use-search-input';

type Category = 'all' | 'dev' | 'daily';

interface Tool {
    path: string;
    title: string;
    description: string;
    icon: typeof FileJson;
    category: Category;
}

const tools: Tool[] = [
    {
        path: '/tools/json',
        title: 'JSON 格式化',
        description: '格式化、驗證和壓縮 JSON',
        icon: FileJson,
        category: 'dev',
    },
    {
        path: '/tools/base64',
        title: 'Base64',
        description: '編碼和解碼 Base64',
        icon: Binary,
        category: 'dev',
    },
    {
        path: '/tools/jwt',
        title: 'JWT 解碼',
        description: '解碼和驗證 JWT Token',
        icon: Key,
        category: 'dev',
    },
    {
        path: '/tools/uuid',
        title: 'UUID 生成器',
        description: '生成 UUID/GUID',
        icon: Hash,
        category: 'dev',
    },
    {
        path: '/tools/timestamp',
        title: '時間戳轉換',
        description: '轉換 Unix 時間戳',
        icon: Clock,
        category: 'daily',
    },
    {
        path: '/tools/snapsplit',
        title: 'SnapSplit 分帳',
        description: '快速分帳計算',
        icon: Receipt,
        category: 'daily',
    },
];

const categories = [
    {key: 'all' as Category, label: '全部', icon: LayoutGrid},
    {key: 'dev' as Category, label: 'Dev 工具', icon: Code},
    {key: 'daily' as Category, label: '日常工具', icon: Wallet},
];

export function HomePage() {
    const [selectedCategory, setSelectedCategory] = useState<Category>('all');
    const [animationKey, setAnimationKey] = useState(0);
    const {value: search, inputProps, isMac} = useSearchInput();

    const filteredTools = useMemo(() => {
        return tools.filter((tool) => {
            if (selectedCategory !== 'all' && tool.category !== selectedCategory) {
                return false;
            }
            if (search) {
                const searchLower = search.toLowerCase();
                return (
                    tool.title.toLowerCase().includes(searchLower) ||
                    tool.description.toLowerCase().includes(searchLower)
                );
            }
            return true;
        });
    }, [search, selectedCategory]);

    // 切換分類時觸發動畫
    const handleCategoryChange = (category: Category) => {
        if (category !== selectedCategory) {
            setSelectedCategory(category);
            setAnimationKey((prev) => prev + 1);
        }
    };

    return (
        <div className="p-6">
            {/* Search Section */}
            <div className="mx-auto max-w-xl mb-8">
                {/* Search Input */}
                <div className="relative">
                    <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground"/>
                    <Input
                        {...inputProps}
                        type="text"
                        placeholder="搜尋工具..."
                        className="pl-10 pr-16 h-11"
                    />
                    <kbd className="absolute right-3 top-1/2 -translate-y-1/2 pointer-events-none hidden sm:inline-flex h-5 select-none items-center gap-1 rounded border bg-muted px-1.5 font-mono text-[10px] font-medium text-muted-foreground">
                        {isMac ? <span className="text-xs">⌘</span> : <span>Ctrl</span>}K
                    </kbd>
                </div>

                {/* Category Tags */}
                <div className="flex flex-wrap justify-center gap-2 mt-4">
                    {categories.map((cat) => (
                        <button
                            key={cat.key}
                            onClick={() => handleCategoryChange(cat.key)}
                            className={cn(
                                'inline-flex items-center gap-1.5 rounded-full px-4 py-1.5 text-sm font-medium transition-colors',
                                selectedCategory === cat.key
                                    ? 'bg-primary text-primary-foreground'
                                    : 'bg-muted text-muted-foreground hover:bg-accent hover:text-accent-foreground'
                            )}
                        >
                            {cat.icon && <cat.icon className="h-3.5 w-3.5"/>}
                            {cat.label}
                        </button>
                    ))}
                </div>
            </div>

            {/* Tools Grid */}
            {filteredTools.length > 0 ? (
                <div key={animationKey} className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                    {filteredTools.map((tool, index) => (
                        <Link
                            key={tool.path}
                            to={tool.path}
                            className="animate-slide-up"
                            style={{'--animation-delay': `${index * 50}ms`} as React.CSSProperties}
                        >
                            <Card className="h-full transition-all duration-200 hover:bg-accent hover:shadow-md hover:-translate-y-0.5">
                                <CardHeader>
                                    <div className="flex items-center gap-3">
                                        <div className="rounded-md bg-primary/10 p-2">
                                            <tool.icon className="h-5 w-5 text-primary"/>
                                        </div>
                                        <div>
                                            <CardTitle className="text-lg">{tool.title}</CardTitle>
                                            <CardDescription>{tool.description}</CardDescription>
                                        </div>
                                    </div>
                                </CardHeader>
                            </Card>
                        </Link>
                    ))}
                </div>
            ) : (
                <div className="text-center py-12 text-muted-foreground animate-fade-in">
                    找不到符合條件的工具
                </div>
            )}
        </div>
    );
}
