/**
 * Logger 工具
 *
 * 統一管理日誌輸出，根據環境自動控制日誌級別：
 * - 開發環境：所有日誌都會輸出
 * - 生產環境：只輸出 warn 和 error
 */

const isDevelopment = import.meta.env.DEV;

type LogLevel = 'debug' | 'info' | 'warn' | 'error';

const LOG_LEVELS: Record<LogLevel, number> = {
    debug: 0,
    info: 1,
    warn: 2,
    error: 3,
};

class Logger {
    private prefix: string;
    private minLevel: LogLevel;

    constructor(prefix: string = '', minLevel?: LogLevel) {
        this.prefix = prefix;
        this.minLevel = minLevel ?? (isDevelopment ? 'debug' : 'warn');
    }

    private shouldLog(level: LogLevel): boolean {
        return LOG_LEVELS[level] >= LOG_LEVELS[this.minLevel];
    }

    private formatPrefix(): string {
        return this.prefix ? `[${this.prefix}]` : '';
    }

    debug(message: string, ...args: unknown[]): void {
        if (this.shouldLog('debug')) {
            console.log(this.formatPrefix(), message, ...args);
        }
    }

    info(message: string, ...args: unknown[]): void {
        if (this.shouldLog('info')) {
            console.info(this.formatPrefix(), message, ...args);
        }
    }

    warn(message: string, ...args: unknown[]): void {
        if (this.shouldLog('warn')) {
            console.warn(this.formatPrefix(), message, ...args);
        }
    }

    error(message: string, ...args: unknown[]): void {
        if (this.shouldLog('error')) {
            console.error(this.formatPrefix(), message, ...args);
        }
    }

    /**
     * 建立子 logger，繼承前綴
     */
    child(prefix: string): Logger {
        const newPrefix = this.prefix ? `${this.prefix}:${prefix}` : prefix;
        return new Logger(newPrefix, this.minLevel);
    }
}

/**
 * 建立新的 logger 實例
 */
export const createLogger = (prefix: string) => new Logger(prefix);

// 預設 logger 實例
export const logger = new Logger();

// 預設功能模組 loggers
export const authLogger = createLogger('Auth');
export const signalRLogger = createLogger('SignalR');
export const collaborationLogger = createLogger('Collaboration');
export const syncLogger = createLogger('Sync');
export const loginLogger = createLogger('Login');
