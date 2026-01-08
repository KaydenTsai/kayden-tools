import {useState} from 'react';
import {Link, Outlet, useLocation} from 'react-router-dom';
import {Binary, Clock, FileJson, Hash, Home, Key, Menu, Moon, Receipt, Sun, X, LogIn, Loader2, User, LogOut} from 'lucide-react';
import {Button} from '@/shared/components/ui/button';
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuLabel,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from '@/shared/components/ui/dropdown-menu';
import {cn} from '@/shared/lib/utils';
import {authLogger} from '@/shared/lib/logger';
import {useThemeStore} from '@/stores/themeStore';
import {useLogin} from '@/shared/hooks/use-login';

const navItems = [
    {path: '/', label: '首頁', icon: Home},
    {path: '/tools/json', label: 'JSON 格式化', icon: FileJson},
    {path: '/tools/base64', label: 'Base64', icon: Binary},
    {path: '/tools/jwt', label: 'JWT 解碼', icon: Key},
    {path: '/tools/timestamp', label: '時間戳', icon: Clock},
    {path: '/tools/uuid', label: 'UUID', icon: Hash},
    {path: '/tools/snapsplit', label: 'Snap Split', icon: Receipt},
];

export function MainLayout() {
    const [sidebarOpen, setSidebarOpen] = useState(false);
    const location = useLocation();
    const {resolvedMode, setMode, mode} = useThemeStore();
    const {login, logout, isLoggingIn, isAuthenticated, user, error: loginError} = useLogin();

    const toggleTheme = () => {
        if (mode === 'system') {
            setMode(resolvedMode === 'light' ? 'dark' : 'light');
        } else {
            setMode(mode === 'light' ? 'dark' : 'light');
        }
    };

    const handleLogin = () => {
        authLogger.debug('Login button clicked');
        login('line');
    };
    const handleLogout = () => logout();

    // 顯示登入錯誤
    if (loginError) {
        authLogger.error('Login error:', loginError);
    }

    return (
        <div className="flex min-h-screen flex-col bg-background">
            {/* Global Header */}
            <header className="sticky top-0 z-50 flex h-14 shrink-0 items-center gap-4 border-b bg-background px-4">
                {/* Mobile menu button */}
                <Button
                    variant="ghost"
                    size="icon"
                    className="lg:hidden"
                    onClick={() => setSidebarOpen(true)}
                >
                    <Menu className="h-5 w-5"/>
                </Button>

                {/* Logo */}
                <Link to="/" className="flex items-center gap-2 font-semibold">
                    Kayden Tools
                </Link>

                {/* Right actions */}
                <div className="ml-auto flex items-center gap-1">
                    <Button variant="ghost" size="icon" onClick={toggleTheme}>
                        {resolvedMode === 'light' ? <Moon className="h-5 w-5"/> : <Sun className="h-5 w-5"/>}
                    </Button>
                    {isAuthenticated ? (
                        <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                                <Button variant="ghost" size="icon" className="rounded-full">
                                    {user?.avatarUrl ? (
                                        <img src={user.avatarUrl} alt="" className="h-7 w-7 rounded-full"/>
                                    ) : (
                                        <User className="h-5 w-5"/>
                                    )}
                                </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end" className="w-56">
                                <DropdownMenuLabel className="font-normal">
                                    <div className="flex flex-col space-y-1">
                                        <p className="text-sm font-medium leading-none">{user?.displayName ?? '用戶'}</p>
                                        {user?.email && (
                                            <p className="text-xs leading-none text-muted-foreground">{user.email}</p>
                                        )}
                                    </div>
                                </DropdownMenuLabel>
                                <DropdownMenuSeparator/>
                                <DropdownMenuItem onClick={handleLogout} className="text-destructive focus:text-destructive">
                                    <LogOut className="mr-2 h-4 w-4"/>
                                    登出
                                </DropdownMenuItem>
                            </DropdownMenuContent>
                        </DropdownMenu>
                    ) : (
                        <Button variant="ghost" size="icon" onClick={handleLogin} disabled={isLoggingIn}>
                            {isLoggingIn ? <Loader2 className="h-5 w-5 animate-spin"/> : <LogIn className="h-5 w-5"/>}
                        </Button>
                    )}
                </div>
            </header>

            <div className="flex flex-1 overflow-hidden">
                {/* Desktop Sidebar */}
                <aside className="hidden w-64 shrink-0 overflow-y-auto border-r bg-background lg:block">
                    <nav className="p-4 space-y-1">
                        {navItems.map((item) => (
                            <Link
                                key={item.path}
                                to={item.path}
                                className={cn(
                                    'flex items-center gap-3 rounded-lg px-3 py-2 text-sm transition-colors',
                                    location.pathname === item.path
                                        ? 'bg-accent text-accent-foreground'
                                        : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground'
                                )}
                            >
                                <item.icon className="h-4 w-4"/>
                                {item.label}
                            </Link>
                        ))}
                    </nav>
                </aside>

                {/* Mobile Sidebar Overlay */}
                {sidebarOpen && (
                    <div className="fixed inset-0 z-50 lg:hidden">
                        <div
                            className="fixed inset-0 bg-black/50"
                            style={{animation: 'overlay-fade-in 0.2s ease-out'}}
                            onClick={() => setSidebarOpen(false)}
                        />
                        <aside
                            className="fixed left-0 top-0 h-full w-64 bg-background"
                            style={{animation: 'slide-in-left 0.2s ease-out'}}
                        >
                            <div className="flex h-14 items-center border-b px-6">
                                <span className="font-semibold">Kayden Tools</span>
                                <Button
                                    variant="ghost"
                                    size="icon"
                                    className="ml-auto"
                                    onClick={() => setSidebarOpen(false)}
                                >
                                    <X className="h-5 w-5"/>
                                </Button>
                            </div>
                            <nav className="p-4 space-y-1">
                                {navItems.map((item) => (
                                    <Link
                                        key={item.path}
                                        to={item.path}
                                        onClick={() => setSidebarOpen(false)}
                                        className={cn(
                                            'flex items-center gap-3 rounded-lg px-3 py-2 text-sm transition-colors',
                                            location.pathname === item.path
                                                ? 'bg-accent text-accent-foreground'
                                                : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground'
                                        )}
                                    >
                                        <item.icon className="h-4 w-4"/>
                                        {item.label}
                                    </Link>
                                ))}
                            </nav>
                        </aside>
                    </div>
                )}

                {/* Main Content */}
                <main className="flex-1 overflow-auto">
                    <Outlet/>
                </main>
            </div>
        </div>
    );
}