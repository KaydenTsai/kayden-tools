/**
 * RFC 7807 Problem Details for HTTP APIs
 * Used by ASP.NET Core for API error responses
 */
export interface ProblemDetails {
    type?: string;
    title: string;
    status: number;
    detail?: string;
    instance?: string;
    errorCode?: string;
    errors?: Record<string, string[]>;
}

/**
 * Application Error class with helper methods
 */
export class AppError extends Error {
    public readonly status: number;
    public readonly errorCode: string;
    public readonly validationErrors?: Record<string, string[]>;

    constructor(problem: ProblemDetails) {
        super(problem.detail || problem.title);
        this.name = 'AppError';
        this.status = problem.status;
        this.errorCode = problem.errorCode || 'UNKNOWN_ERROR';
        this.validationErrors = problem.errors;
    }

    get isNotFound(): boolean {
        return this.status === 404;
    }

    get isValidationError(): boolean {
        return this.status === 400 && !!this.validationErrors;
    }

    get isUnauthorized(): boolean {
        return this.status === 401;
    }

    get isForbidden(): boolean {
        return this.status === 403;
    }

    get isServerError(): boolean {
        return this.status >= 500;
    }
}

/**
 * Type guard to check if an error is ProblemDetails
 */
export function isProblemDetails(error: unknown): error is ProblemDetails {
    return (
        typeof error === 'object' &&
        error !== null &&
        'status' in error &&
        'title' in error
    );
}

/**
 * Type guard to check if an error is AppError
 */
export function isAppError(error: unknown): error is AppError {
    return error instanceof AppError;
}

/**
 * Convert any error to a user-friendly message
 */
export function getErrorMessage(error: unknown): string {
    if (error instanceof AppError) {
        return error.message;
    }
    if (isProblemDetails(error)) {
        return error.detail || error.title || '發生錯誤';
    }
    if (error instanceof Error) {
        return error.message;
    }
    if (typeof error === 'string') {
        return error;
    }
    return '發生未知錯誤';
}

/**
 * Error codes for common scenarios
 */
export const ErrorCodes = {
    // Network errors
    NETWORK_ERROR: 'NETWORK_ERROR',
    TIMEOUT: 'TIMEOUT',

    // Auth errors
    UNAUTHORIZED: 'UNAUTHORIZED',
    FORBIDDEN: 'FORBIDDEN',

    // Validation errors
    VALIDATION_ERROR: 'VALIDATION_ERROR',

    // Resource errors
    NOT_FOUND: 'NOT_FOUND',
    CONFLICT: 'CONFLICT',

    // Server errors
    SERVER_ERROR: 'SERVER_ERROR',

    // Client errors
    UNKNOWN: 'UNKNOWN',
} as const;

export type ErrorCode = (typeof ErrorCodes)[keyof typeof ErrorCodes];
