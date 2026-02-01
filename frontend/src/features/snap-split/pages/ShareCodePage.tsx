import {useEffect, useState} from 'react';
import {useParams, useNavigate} from 'react-router-dom';
import {AlertCircle, Loader2} from 'lucide-react';
import {Button} from '@/shared/components/ui/button';
import {Card, CardContent} from '@/shared/components/ui/card';
import {useBillSync} from '@/features/snap-split/hooks/useBillSync';
import {useSnapSplitStore} from '@/features/snap-split/stores/snapSplitStore';

export function ShareCodePage() {
    const {shareCode} = useParams<{ shareCode: string }>();
    const navigate = useNavigate();
    const {fetchBillByShareCode, isDownloading, downloadError} = useBillSync();
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const loadBill = async () => {
            if (!shareCode) {
                setError('缺少分享碼');
                setIsLoading(false);
                return;
            }

            // 從 store 快照讀取，避免將 bills 加入依賴陣列
            const bills = useSnapSplitStore.getState().bills;
            const existingBill = bills.find(b => b.shareCode === shareCode);
            if (existingBill) {
                navigate('/tools/snapsplit', {replace: true});
                return;
            }

            try {
                await fetchBillByShareCode(shareCode);
                navigate('/tools/snapsplit', {replace: true});
            } catch {
                setError('無法載入帳單，分享碼可能無效或已過期。');
            } finally {
                setIsLoading(false);
            }
        };

        loadBill();
    }, [shareCode, fetchBillByShareCode, navigate]);

    const handleGoHome = () => {
        navigate('/tools/snapsplit', {replace: true});
    };

    // 載入中
    if (isLoading || isDownloading) {
        return (
            <div className="min-h-screen flex items-center justify-center bg-background p-4">
                <Card className="w-full max-w-sm">
                    <CardContent className="pt-6 text-center">
                        <Loader2 className="h-12 w-12 animate-spin text-primary mx-auto mb-4"/>
                        <h2 className="text-lg font-semibold mb-2">載入帳單中...</h2>
                        <p className="text-sm text-muted-foreground">
                            分享碼：{shareCode}
                        </p>
                    </CardContent>
                </Card>
            </div>
        );
    }

    // 錯誤狀態
    if (error || downloadError) {
        return (
            <div className="min-h-screen flex items-center justify-center bg-background p-4">
                <Card className="w-full max-w-sm">
                    <CardContent className="pt-6 text-center">
                        <div className="mb-4 p-4 bg-destructive/10 border border-destructive/30 rounded-lg">
                            <div className="flex items-center justify-center gap-2 text-destructive">
                                <AlertCircle className="h-5 w-5"/>
                                <span className="font-medium">載入失敗</span>
                            </div>
                            <p className="text-sm text-destructive mt-2">
                                {error || downloadError?.message || '無法載入帳單'}
                            </p>
                        </div>
                        <Button onClick={handleGoHome} className="w-full">
                            前往 SnapSplit
                        </Button>
                    </CardContent>
                </Card>
            </div>
        );
    }

    return null;
}
