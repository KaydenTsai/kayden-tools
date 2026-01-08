import {useState, useEffect, useRef, useCallback} from 'react';

interface UseSearchInputOptions {
    onSearch?: (value: string) => void;
}

export function useSearchInput(options: UseSearchInputOptions = {}) {
    const {onSearch} = options;
    const [value, setValue] = useState('');
    const [isMac, setIsMac] = useState(true);
    const inputRef = useRef<HTMLInputElement>(null);

    useEffect(() => {
        setIsMac(navigator.platform.toUpperCase().includes('MAC'));

        const handleKeyDown = (e: KeyboardEvent) => {
            if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
                e.preventDefault();
                inputRef.current?.focus();
            }
        };

        document.addEventListener('keydown', handleKeyDown);
        return () => document.removeEventListener('keydown', handleKeyDown);
    }, []);

    const handleChange = useCallback((newValue: string) => {
        setValue(newValue);
        onSearch?.(newValue);
    }, [onSearch]);

    const handleKeyDown = useCallback((e: React.KeyboardEvent<HTMLInputElement>) => {
        if (e.key === 'Escape') {
            setValue('');
            onSearch?.('');
            inputRef.current?.blur();
        }
    }, [onSearch]);

    const clear = useCallback(() => {
        setValue('');
        onSearch?.('');
    }, [onSearch]);

    const focus = useCallback(() => {
        inputRef.current?.focus();
    }, []);

    return {
        value,
        setValue: handleChange,
        inputRef,
        inputProps: {
            ref: inputRef,
            value,
            onChange: (e: React.ChangeEvent<HTMLInputElement>) => handleChange(e.target.value),
            onKeyDown: handleKeyDown,
        },
        isMac,
        clear,
        focus,
    };
}