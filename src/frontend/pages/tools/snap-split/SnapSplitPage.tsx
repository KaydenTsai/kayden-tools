import { BillListView } from "./views/BillListView";
import { BillDetailView } from "./views/BillDetailView";
import { useEffect, useState, useMemo } from "react";
import { Alert, Box, Button, Chip, Tooltip } from "@mui/material";
import {
    Save as SaveIcon,
    CameraAlt as SnapshotIcon,
    Link as LinkIcon,
} from "@mui/icons-material";
import type { Bill } from "@/types/snap-split";
import { toolsById } from "@/utils/tools";
import { useCurrentBill, useSnapSplitStore } from "@/stores/snapSplitStore";
import { useAuthStore } from "@/stores/authStore";
import { clearShareHash, decodeBillFromUrl } from "@/utils/shareUrl";
import { ToolPageLayout } from "@/components/ui/ToolPageLayout";

export function SnapSplitPage() {
    const tool = toolsById['snapsplit'];
    const { selectBill, importBillFromSnapshot } = useSnapSplitStore();
    const { isAuthenticated } = useAuthStore();
    const currentBill = useCurrentBill();
    const [previewBill, setPreviewBill] = useState<Bill | null>(null);

    // 取得分享來源資訊（用於快照匯入）
    const shareSource = useMemo(() => {
        const hashQuery = window.location.hash.split('?')[1];
        const params = new URLSearchParams(hashQuery || window.location.search);
        return params.get('snap') ? `快照連結 (${new Date().toLocaleDateString()})` : undefined;
    }, []);

    useEffect(() => {
        const sharedBill = decodeBillFromUrl();
        if (sharedBill) {
            setPreviewBill(sharedBill);
        }
    }, []);

    const handleBack = () => {
        if (previewBill) {
            setPreviewBill(null);
            clearShareHash();
        } else {
            selectBill('');
        }
    };

    const handleImport = () => {
        if (previewBill) {
            importBillFromSnapshot(previewBill, shareSource);
            setPreviewBill(null);
            clearShareHash();
        }
    };

    const displayBill = previewBill ?? currentBill;

    return (
        <ToolPageLayout
            title={tool.name}
            description={tool.description}
            disablePaperWrapper={!!displayBill}
        >
            {previewBill && (
                <Alert
                    severity="info"
                    icon={<SnapshotIcon />}
                    sx={{ mb: 2, borderRadius: 2 }}
                    action={
                        <Button
                            color="inherit"
                            size="small"
                            startIcon={<SaveIcon />}
                            onClick={handleImport}
                        >
                            儲存至我的帳單
                        </Button>
                    }
                >
                    <Box component="span" sx={{ fontWeight: 600 }}>這是一份快照帳單</Box>
                    <Box component="span" sx={{ ml: 1, color: 'text.secondary' }}>
                        （靜態副本，與原作者斷開連結）
                    </Box>
                </Alert>
            )}

            {displayBill?.isSnapshot && !previewBill && (
                <Alert
                    severity="warning"
                    icon={<SnapshotIcon />}
                    sx={{ mb: 2, borderRadius: 2 }}
                >
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                        <Box component="span" sx={{ fontWeight: 600 }}>
                            這是從快照匯入的帳單
                        </Box>
                        {displayBill.snapshotSource && (
                            <Tooltip title={`來源：${displayBill.snapshotSource}`}>
                                <Chip
                                    icon={<LinkIcon />}
                                    label="快照"
                                    size="small"
                                    variant="outlined"
                                />
                            </Tooltip>
                        )}
                    </Box>
                    <Box component="div" sx={{ mt: 0.5, color: 'text.secondary', fontSize: '0.875rem' }}>
                        你可以自由編輯，但不會與原作者同步。
                    </Box>
                </Alert>
            )}

            {displayBill ? (
                <BillDetailView
                    bill={displayBill}
                    onBack={handleBack}
                    isReadOnly={!!previewBill}
                    isAuthenticated={isAuthenticated}
                />
            ) : (
                <BillListView />
            )}
        </ToolPageLayout>
    );
}
