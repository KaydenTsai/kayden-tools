import { Avatar, Box, Stack, Typography, Zoom } from "@mui/material";
import { Edit as EditIcon } from "@mui/icons-material";

interface UserAvatarProps {
    name: string;
    color: string;
    selected?: boolean;
    onClick?: () => void;
    size?: number; // Default 48
}

export function UserAvatar({ name, color, selected, onClick, size = 48 }: UserAvatarProps) {
    return (
        <Stack alignItems="center" spacing={0.5} sx={{ width: size + 16 }}>
            <Box
                onClick={onClick}
                sx={{
                    position: 'relative',
                    cursor: onClick ? 'pointer' : 'default',
                    p: 0.5,
                    borderRadius: '50%',
                    border: '2px solid',
                    borderColor: selected ? 'primary.main' : 'transparent',
                    transition: 'all 0.2s',
                }}
            >
                <Avatar
                    sx={{
                        bgcolor: color,
                        width: size,
                        height: size,
                        fontSize: `${size / 40}rem`,
                        fontWeight: 600,
                        transition: 'transform 0.1s',
                        '&:active': onClick ? { transform: 'scale(0.95)' } : {},
                    }}
                >
                    {name.charAt(0).toUpperCase()}
                </Avatar>
                {selected && (
                    <Zoom in>
                        <Box
                            sx={{
                                position: 'absolute',
                                bottom: 0,
                                right: 0,
                                bgcolor: 'primary.main',
                                color: 'white',
                                borderRadius: '50%',
                                width: size * 0.4,
                                height: size * 0.4,
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                border: '2px solid',
                                borderColor: 'background.paper',
                            }}
                        >
                            <EditIcon sx={{ fontSize: size * 0.25 }} />
                        </Box>
                    </Zoom>
                )}
            </Box>
            <Typography
                variant="caption"
                noWrap
                align="center"
                fontWeight={selected ? 700 : 400}
                color={selected ? 'primary.main' : 'text.primary'}
                sx={{
                    width: '100%',
                    px: 0.5,
                    opacity: selected ? 1 : 0.8
                }}
            >
                {name}
            </Typography>
        </Stack>
    );
}
