import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Box, CircularProgress, Typography, Alert, Button, Paper } from '@mui/material';
import { useBillSync } from '@/hooks/useBillSync';
import { useSnapSplitStore } from '@/stores/snapSplitStore';

export function ShareCodePage() {
    const { shareCode } = useParams<{ shareCode: string }>();
    const navigate = useNavigate();
    const { fetchBillByShareCode, isDownloading, downloadError } = useBillSync();
    const { bills } = useSnapSplitStore();
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const loadBill = async () => {
            if (!shareCode) {
                setError('Missing share code');
                setIsLoading(false);
                return;
            }

            // Check if we already have this bill locally
            const existingBill = bills.find(b => b.shareCode === shareCode);
            if (existingBill) {
                // Navigate to the existing bill
                navigate('/tools/snapsplit', { replace: true });
                return;
            }

            try {
                await fetchBillByShareCode(shareCode);
                // After import, navigate to SnapSplit
                navigate('/tools/snapsplit', { replace: true });
            } catch {
                setError('Failed to load bill. The share code may be invalid or expired.');
            } finally {
                setIsLoading(false);
            }
        };

        loadBill();
    }, [shareCode, fetchBillByShareCode, bills, navigate]);

    const handleGoHome = () => {
        navigate('/tools/snapsplit', { replace: true });
    };

    if (isLoading || isDownloading) {
        return (
            <Box
                sx={{
                    minHeight: '100vh',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    bgcolor: 'background.default',
                    p: 3,
                }}
            >
                <Paper sx={{ p: 4, maxWidth: 400, width: '100%', textAlign: 'center' }}>
                    <CircularProgress size={48} sx={{ mb: 3 }} />
                    <Typography variant="h6" gutterBottom>
                        Loading Bill...
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                        Share Code: {shareCode}
                    </Typography>
                </Paper>
            </Box>
        );
    }

    if (error || downloadError) {
        return (
            <Box
                sx={{
                    minHeight: '100vh',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    bgcolor: 'background.default',
                    p: 3,
                }}
            >
                <Paper sx={{ p: 4, maxWidth: 400, width: '100%', textAlign: 'center' }}>
                    <Alert severity="error" sx={{ mb: 3 }}>
                        {error || downloadError?.message || 'Failed to load bill'}
                    </Alert>
                    <Button
                        variant="contained"
                        onClick={handleGoHome}
                        fullWidth
                    >
                        Go to SnapSplit
                    </Button>
                </Paper>
            </Box>
        );
    }

    return null;
}
