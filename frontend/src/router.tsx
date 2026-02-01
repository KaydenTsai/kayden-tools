import {createHashRouter} from 'react-router-dom';
import {MainLayout} from '@/shared/components/layout/MainLayout';
import {HomePage} from '@/tools/home/HomePage';
import {SnapSplitPage} from '@/features/snap-split/pages/SnapSplitPage';
import {ShareCodePage} from '@/features/snap-split/pages/ShareCodePage';
import {AuthCallbackPage} from '@/shared/pages/AuthCallbackPage';

// 暫時的佔位頁面
const PlaceholderPage = ({name}: { name: string }) => (
    <div className="p-6">
        <h1 className="text-2xl font-bold">{name}</h1>
        <p className="text-muted-foreground mt-2">此頁面正在遷移中...</p>
    </div>
);

export const router = createHashRouter([
    {
        path: '/',
        element: <MainLayout/>,
        children: [
            {
                index: true,
                element: <HomePage/>,
            },
            {
                path: 'tools/json',
                element: <PlaceholderPage name="JSON Formatter"/>,
            },
            {
                path: 'tools/base64',
                element: <PlaceholderPage name="Base64 Encoder/Decoder"/>,
            },
            {
                path: 'tools/jwt',
                element: <PlaceholderPage name="JWT Decoder"/>,
            },
            {
                path: 'tools/timestamp',
                element: <PlaceholderPage name="Timestamp Converter"/>,
            },
            {
                path: 'tools/uuid',
                element: <PlaceholderPage name="UUID Generator"/>,
            },
            {
                path: 'tools/snapsplit',
                element: <SnapSplitPage/>,
            },
            {
                path: 'snap-split/share/:shareCode',
                element: <ShareCodePage/>,
            },
            {
                path: 'auth/callback',
                element: <AuthCallbackPage/>,
            },
        ],
    },
]);