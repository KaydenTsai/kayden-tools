import {create} from 'zustand';
import {persist} from 'zustand/middleware';

type ThemeMode = 'light' | 'dark' | 'system';

interface ThemeState {
    mode: ThemeMode;
    setMode: (mode: ThemeMode) => void;
    resolvedMode: 'light' | 'dark';
}

const getSystemTheme = (): 'light' | 'dark' => {
    if (typeof window === 'undefined') return 'light';
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
};

export const useThemeStore = create<ThemeState>()(
    persist(
        (set) => ({
            mode: 'system',
            resolvedMode: getSystemTheme(),
            setMode: (mode: ThemeMode) => {
                const resolvedMode = mode === 'system' ? getSystemTheme() : mode;
                set({mode, resolvedMode});
            },
        }),
        {
            name: 'kayden-tools-theme',
            onRehydrateStorage: () => (state) => {
                if (state) {
                    const resolvedMode = state.mode === 'system' ? getSystemTheme() : state.mode;
                    state.resolvedMode = resolvedMode;
                }
            },
        }
    )
);

// 監聽系統主題變化
if (typeof window !== 'undefined') {
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
        const state = useThemeStore.getState();
        if (state.mode === 'system') {
            useThemeStore.setState({resolvedMode: e.matches ? 'dark' : 'light'});
        }
    });
}