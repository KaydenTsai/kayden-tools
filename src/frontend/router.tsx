import { createHashRouter } from 'react-router-dom';
import { MainLayout } from '@/layouts/MainLayout';
import { HomePage } from '@/pages/home/HomePage';
import { JsonFormatterPage } from '@/pages/tools/json-formatter/JsonFormatterPage';
import { Base64Page } from '@/pages/tools/base64/Base64Page';
import { JwtDecoderPage } from '@/pages/tools/jwt-decoder/JwtDecoderPage';
import { TimestampPage } from '@/pages/tools/timestamp/TimestampPage';
import { UuidGeneratorPage } from '@/pages/tools/uuid-generator/UuidGeneratorPage';
import { SnapSplitPage } from '@/pages/tools/snap-split/SnapSplitPage';
import { ShareCodePage } from '@/pages/tools/snap-split/ShareCodePage';
import { AuthCallbackPage } from '@/pages/auth/AuthCallbackPage';

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
                element: <JsonFormatterPage/>,
            },
            {
                path: 'tools/base64',
                element: <Base64Page/>,
            },
            {
                path: 'tools/jwt',
                element: <JwtDecoderPage/>,
            },
            {
                path: 'tools/timestamp',
                element: <TimestampPage/>,
            },
            {
                path: 'tools/uuid',
                element: <UuidGeneratorPage/>,
            },
            {
                path: 'tools/snapsplit',
                element: <SnapSplitPage/>
            },
            {
                path: 'snap-split/share/:shareCode',
                element: <ShareCodePage/>
            },
        ],
    },
    {
        path: '/auth/callback',
        element: <AuthCallbackPage/>,
    },
]);
