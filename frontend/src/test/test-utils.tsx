import type {ReactElement, ReactNode} from 'react';
import {render} from '@testing-library/react';
import type {RenderOptions} from '@testing-library/react';
import {BrowserRouter} from 'react-router-dom';

interface WrapperProps {
    children: ReactNode;
}

// Default providers wrapper for tests
function AllTheProviders({children}: WrapperProps) {
    return <BrowserRouter>{children}</BrowserRouter>;
}

// Custom render function that includes providers
function customRender(ui: ReactElement, options?: Omit<RenderOptions, 'wrapper'>) {
    return render(ui, {wrapper: AllTheProviders, ...options});
}

// Re-export everything from testing-library
export * from '@testing-library/react';
export {customRender as render};
