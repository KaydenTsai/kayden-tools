import { BillListView } from "./views/BillListView";
import { BillDetailView } from "./views/BillDetailView";
import { useEffect, useState } from "react";
import { Alert, Box, Button } from "@mui/material";
import { Save as SaveIcon } from "@mui/icons-material";
import type { Bill } from "@/types/snap-split";
import { toolsById } from "@/utils/tools";
import { useCurrentBill, useSnapSplitStore } from "@/stores/snapSplitStore";
import { useAuthStore } from "@/stores/authStore";
import { clearShareHash, decodeBillFromUrl } from "@/utils/shareUrl";
import { ToolPageLayout } from "@/components/ui/ToolPageLayout";

export function SnapSplitPage() {
    const tool = toolsById['snapsplit'];
    const { selectBill, importBill } = useSnapSplitStore();
    const { isAuthenticated } = useAuthStore();
    const currentBill = useCurrentBill();
    const [previewBill, setPreviewBill] = useState<Bill | null>(null);

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
            importBill(previewBill);
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
                    sx={{ mb: 2, borderRadius: 2 }}
                    action={
                        <Button
                            color="inherit"
                            size="small"
                            startIcon={<SaveIcon />}
                            onClick={handleImport}
                        >
                            儲存到本地
                        </Button>
                    }
                >
                    <Box component="span" sx={{ fontWeight: 600 }}>這是一份分享的帳單</Box>
                    <Box component="span" sx={{ ml: 1 }}>（唯讀模式，修改不會影響原始連結）</Box>
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
