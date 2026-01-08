import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '@/shared/components/ui/dialog';
import { Button } from '@/shared/components/ui/button';
import { Cloud, HardDrive, Trash2 } from 'lucide-react';

interface DeleteBillDialogProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    billName: string;
    onConfirm: (deleteFromCloud: boolean) => void;
    isDeleting?: boolean;
}

export function DeleteBillDialog({
    open,
    onOpenChange,
    billName,
    onConfirm,
    isDeleting = false,
}: DeleteBillDialogProps) {
    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="sm:max-w-md">
                <DialogHeader>
                    <DialogTitle className="flex items-center gap-2">
                        <Trash2 className="h-5 w-5 text-destructive" />
                        刪除帳單
                    </DialogTitle>
                    <DialogDescription>
                        確定要刪除「{billName}」嗎？此帳單已同步到雲端。
                    </DialogDescription>
                </DialogHeader>

                <div className="flex flex-col gap-3 py-4">
                    <Button
                        variant="destructive"
                        onClick={() => onConfirm(true)}
                        disabled={isDeleting}
                        className="justify-start"
                    >
                        <Cloud className="h-4 w-4 mr-2" />
                        刪除本地和雲端
                    </Button>
                    <p className="text-xs text-muted-foreground ml-6">
                        完全刪除，無法復原
                    </p>

                    <Button
                        variant="outline"
                        onClick={() => onConfirm(false)}
                        disabled={isDeleting}
                        className="justify-start"
                    >
                        <HardDrive className="h-4 w-4 mr-2" />
                        只刪除本地
                    </Button>
                    <p className="text-xs text-muted-foreground ml-6">
                        保留雲端資料，日後可重新下載
                    </p>
                </div>

                <DialogFooter>
                    <Button
                        variant="ghost"
                        onClick={() => onOpenChange(false)}
                        disabled={isDeleting}
                    >
                        取消
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
