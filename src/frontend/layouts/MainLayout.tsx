import { type ReactNode, useState, useEffect } from 'react';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import {
  AppBar,
  Avatar,
  Box,
  Collapse,
  Drawer,
  IconButton,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Toolbar,
  Typography,
  useTheme,
  useMediaQuery,
  Divider,
  Tooltip,
} from '@mui/material';
import {
  Menu as MenuIcon,
  Home as HomeIcon,
  DarkMode as DarkModeIcon,
  LightMode as LightModeIcon,
  DataObject as DataObjectIcon,
  Code as CodeIcon,
  Key as KeyIcon,
  Schedule as ScheduleIcon,
  Fingerprint as FingerprintIcon,
  Calculate as CalculateIcon,
  ExpandLess as ExpandLessIcon,
  ExpandMore as ExpandMoreIcon,
  Terminal as TerminalIcon,
  Today as TodayIcon,
  AccountCircle as AccountCircleIcon,
} from '@mui/icons-material';
import { useThemeStore } from '@/stores/themeStore';
import { useAuthStore } from '@/stores/authStore';
import { tools } from '@/utils/tools';
import { categoryLabels, type ToolCategory } from '@/types/tool';
import { useAuthHandshake } from '@/hooks/useLoginHandshake';
import { SyncHandshakeDialog } from '@/components/dialogs/SyncHandshakeDialog';
import { LoginDialog } from '@/components/dialogs/LoginDialog';

const DRAWER_WIDTH = 230;

const iconMap: Record<string, ReactNode> = {
  DataObject: <DataObjectIcon />,
  Code: <CodeIcon />,
  Key: <KeyIcon />,
  Schedule: <ScheduleIcon />,
  Fingerprint: <FingerprintIcon />,
  Calculate: <CalculateIcon />,
};

const categoryIcons: Record<ToolCategory, ReactNode> = {
  dev: <TerminalIcon />,
  daily: <TodayIcon />,
};

// 依類型分組工具
const toolsByCategory = tools.reduce((acc, tool) => {
  if (!acc[tool.category]) {
    acc[tool.category] = [];
  }
  acc[tool.category].push(tool);
  return acc;
}, {} as Record<ToolCategory, typeof tools>);

const categories = Object.keys(toolsByCategory) as ToolCategory[];

export function MainLayout() {
  const theme = useTheme();
  const navigate = useNavigate();
  const location = useLocation();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [mobileOpen, setMobileOpen] = useState(false);
  const [loginDialogOpen, setLoginDialogOpen] = useState(false);
  const [expandedCategories, setExpandedCategories] = useState<Record<ToolCategory, boolean>>(
    () => categories.reduce((acc, cat) => ({ ...acc, [cat]: true }), {} as Record<ToolCategory, boolean>)
  );
  const { resolvedMode, setMode } = useThemeStore();
  const { isAuthenticated, user, initialize: initializeAuth } = useAuthStore();

  // Initialize auth state on mount
  useEffect(() => {
    initializeAuth();
  }, [initializeAuth]);

  const {
    showDialog: showSyncDialog,
    unsyncedBills,
    syncProgress,
    dismissDialog: dismissSyncDialog,
    syncSelectedBills,
    syncAllBills,
  } = useAuthHandshake(isAuthenticated);

  const toggleCategory = (category: ToolCategory) => {
    setExpandedCategories(prev => ({ ...prev, [category]: !prev[category] }));
  };

  const handleDrawerToggle = () => {
    setMobileOpen(!mobileOpen);
  };

  const handleNavigation = (path: string) => {
    navigate(path);
    if (isMobile) {
      setMobileOpen(false);
    }
  };

  const toggleTheme = () => {
    setMode(resolvedMode === 'light' ? 'dark' : 'light');
  };

  const drawerContent = (
    <Box sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
      <Toolbar>
        <Typography
          variant="h6"
          sx={{
            fontWeight: 700,
            cursor: 'pointer',
            color: 'text.primary',
          }}
          onClick={() => handleNavigation('/')}
        >
          Kayden Tools
        </Typography>
      </Toolbar>
      <Divider />
      <List sx={{ flex: 1, px: 1 }}>
        <ListItem disablePadding>
          <ListItemButton
            selected={location.pathname === '/'}
            onClick={() => handleNavigation('/')}
            sx={{ borderRadius: 2, mb: 0.5 }}
          >
            <ListItemIcon sx={{ minWidth: 40 }}>
              <HomeIcon />
            </ListItemIcon>
            <ListItemText primary="首頁" />
          </ListItemButton>
        </ListItem>
        <Divider sx={{ my: 1 }} />
        {categories.map((category) => (
          <Box key={category}>
            <ListItemButton
              onClick={() => toggleCategory(category)}
              sx={{ borderRadius: 2, mb: 0.5 }}
            >
              <ListItemIcon sx={{ minWidth: 40 }}>
                {categoryIcons[category]}
              </ListItemIcon>
              <ListItemText
                primary={categoryLabels[category]}
                primaryTypographyProps={{ fontWeight: 500, fontSize: '0.9rem' }}
              />
              {expandedCategories[category] ? <ExpandLessIcon /> : <ExpandMoreIcon />}
            </ListItemButton>
            <Collapse in={expandedCategories[category]} timeout="auto" unmountOnExit>
              <List disablePadding>
                {toolsByCategory[category].map((tool) => (
                  <ListItem key={tool.id} disablePadding>
                    <ListItemButton
                      selected={location.pathname === tool.path}
                      onClick={() => handleNavigation(tool.path)}
                      sx={{ borderRadius: 2, mb: 0.5, pl: 4 }}
                    >
                      <ListItemIcon sx={{ minWidth: 36 }}>
                        {iconMap[tool.icon] || <DataObjectIcon />}
                      </ListItemIcon>
                      <ListItemText
                        primary={tool.name}
                        primaryTypographyProps={{ fontSize: '0.875rem' }}
                      />
                    </ListItemButton>
                  </ListItem>
                ))}
              </List>
            </Collapse>
          </Box>
        ))}
      </List>
    </Box>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <AppBar
        position="fixed"
        elevation={0}
        sx={{
          width: { md: `calc(100% - ${DRAWER_WIDTH}px)` },
          ml: { md: `${DRAWER_WIDTH}px` },
          bgcolor: 'background.paper',
          borderBottom: 1,
          borderColor: 'divider',
        }}
      >
        <Toolbar>
          <IconButton
            color="inherit"
            edge="start"
            onClick={handleDrawerToggle}
            sx={{ mr: 1, display: { md: 'none' }, color: 'text.primary' }}
          >
            <MenuIcon />
          </IconButton>
          {/* 手機版：非首頁時顯示首頁按鈕 */}
          {isMobile && location.pathname !== '/' && (
            <Tooltip title="返回首頁">
              <IconButton
                onClick={() => navigate('/')}
                sx={{ color: 'text.primary' }}
              >
                <HomeIcon />
              </IconButton>
            </Tooltip>
          )}
          <Box sx={{ flex: 1 }} />
          <Tooltip title={resolvedMode === 'light' ? '深色模式' : '淺色模式'}>
            <IconButton onClick={toggleTheme} sx={{ color: 'text.primary' }}>
              {resolvedMode === 'light' ? <DarkModeIcon /> : <LightModeIcon />}
            </IconButton>
          </Tooltip>
          <Tooltip title={isAuthenticated ? user?.displayName || '帳戶' : '登入'}>
            <IconButton onClick={() => setLoginDialogOpen(true)} sx={{ color: 'text.primary', ml: 0.5 }}>
              {isAuthenticated ? (
                <Avatar
                  src={user?.avatarUrl || undefined}
                  sx={{ width: 28, height: 28, fontSize: '0.875rem' }}
                >
                  {user?.displayName?.charAt(0) || 'U'}
                </Avatar>
              ) : (
                <AccountCircleIcon />
              )}
            </IconButton>
          </Tooltip>
        </Toolbar>
      </AppBar>

      <Box
        component="nav"
        sx={{ width: { md: DRAWER_WIDTH }, flexShrink: { md: 0 } }}
      >
        {/* Mobile drawer */}
        <Drawer
          variant="temporary"
          open={mobileOpen}
          onClose={handleDrawerToggle}
          ModalProps={{ keepMounted: true }}
          sx={{
            display: { xs: 'block', md: 'none' },
            '& .MuiDrawer-paper': {
              boxSizing: 'border-box',
              width: DRAWER_WIDTH,
            },
          }}
        >
          {drawerContent}
        </Drawer>
        {/* Desktop drawer */}
        <Drawer
          variant="permanent"
          sx={{
            display: { xs: 'none', md: 'block' },
            '& .MuiDrawer-paper': {
              boxSizing: 'border-box',
              width: DRAWER_WIDTH,
              borderRight: 1,
              borderColor: 'divider',
            },
          }}
          open
        >
          {drawerContent}
        </Drawer>
      </Box>

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          width: { md: `calc(100% - ${DRAWER_WIDTH}px)` },
          minHeight: '100vh',
          bgcolor: 'background.default',
        }}
      >
        <Toolbar />
        <Box sx={{ p: { xs: 2, md: 3 } }}>
          <Outlet />
        </Box>
      </Box>

      <SyncHandshakeDialog
        open={showSyncDialog}
        onClose={dismissSyncDialog}
        unsyncedBills={unsyncedBills}
        syncProgress={syncProgress}
        onSyncSelected={syncSelectedBills}
        onSyncAll={syncAllBills}
      />

      <LoginDialog
        open={loginDialogOpen}
        onClose={() => setLoginDialogOpen(false)}
      />
    </Box>
  );
}
