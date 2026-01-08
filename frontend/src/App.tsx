import { RouterProvider } from 'react-router-dom';
import { router } from './router';
import { useThemeStore } from '@/stores/themeStore';
import { useAuthStore } from '@/stores/authStore';
import { useEffect } from 'react';
import { Toaster } from '@/shared/components/ui/toaster';

function App() {
    const resolvedMode = useThemeStore((state) => state.resolvedMode);
    const isHydrated = useAuthStore((state) => state.isHydrated);

    useEffect(() => {
        // Apply dark mode class to document
        if (resolvedMode === 'dark') {
            document.documentElement.classList.add('dark');
        } else {
            document.documentElement.classList.remove('dark');
        }
    }, [resolvedMode]);

    // 等待 Zustand persist hydration 完成，避免 UI 閃爍
    if (!isHydrated) {
        return (
            <div className="flex items-center justify-center min-h-screen bg-background">
                <div className="animate-pulse text-muted-foreground">載入中...</div>
            </div>
        );
    }

    return (
        <>
            <RouterProvider router={router}/>
            <Toaster />
        </>
    );
}

export default App;
