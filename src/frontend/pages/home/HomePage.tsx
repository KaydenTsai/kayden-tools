import { useState, useMemo, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Box,
  Card,
  CardActionArea,
  CardContent,
  Grid,
  InputAdornment,
  TextField,
  Typography,
  Chip,
  useTheme,
  ToggleButton,
  ToggleButtonGroup,
  keyframes,
} from '@mui/material';
import {
  Search as SearchIcon,
  DataObject as DataObjectIcon,
  Code as CodeIcon,
  Key as KeyIcon,
  Schedule as ScheduleIcon,
  Fingerprint as FingerprintIcon,
  Calculate as CalculateIcon,
} from '@mui/icons-material';
import { tools, searchTools } from '@/utils/tools';
import { categoryLabels, type ToolCategory } from '@/types/tool';
import { colors } from '@/theme';

const iconMap: Record<string, ReactNode> = {
  DataObject: <DataObjectIcon fontSize="large" />,
  Code: <CodeIcon fontSize="large" />,
  Key: <KeyIcon fontSize="large" />,
  Schedule: <ScheduleIcon fontSize="large" />,
  Fingerprint: <FingerprintIcon fontSize="large" />,
  Calculate: <CalculateIcon fontSize="large" />,
};

const categoryColors: Record<ToolCategory, string> = {
  dev: colors.category.dev,
  daily: colors.category.daily,
};

const expandIn = keyframes`
  0% {
    opacity: 0;
    transform: scale(0.8) translateY(-20px);
  }
  100% {
    opacity: 1;
    transform: scale(1) translateY(0);
  }
`;

export function HomePage() {
  const theme = useTheme();
  const navigate = useNavigate();
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<ToolCategory | 'all'>('all');
  const [animationKey, setAnimationKey] = useState(0);

  const handleCategoryChange = (_: React.MouseEvent<HTMLElement>, value: ToolCategory | 'all' | null) => {
    if (value && value !== selectedCategory) {
      setSelectedCategory(value);
      setAnimationKey(prev => prev + 1);
    }
  };

  const filteredTools = useMemo(() => {
    let result = tools;
    if (selectedCategory !== 'all') {
      result = result.filter(tool => tool.category === selectedCategory);
    }
    if (searchQuery.trim()) {
      const searchResult = searchTools(searchQuery);
      result = result.filter(tool => searchResult.some(t => t.id === tool.id));
    }
    return result;
  }, [searchQuery, selectedCategory]);

  return (
    <Box>
      <Box sx={{ mb: { xs: 3, md: 4 }, textAlign: 'center' }}>
        <Typography
          variant="h3"
          component="h1"
          sx={{
            fontWeight: 700,
            mb: 1,
            color: 'text.primary',
            fontSize: { xs: '1.75rem', md: '3rem' },
          }}
        >
          Kayden Tools
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: { xs: 2, md: 3 } }}>
          實用工具集
        </Typography>
        <TextField
          fullWidth
          placeholder="搜尋工具..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          sx={{ maxWidth: 500, mb: 2 }}
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon color="action" />
              </InputAdornment>
            ),
          }}
        />
        <Box>
          <ToggleButtonGroup
            value={selectedCategory}
            exclusive
            onChange={handleCategoryChange}
            size="small"
            sx={{
              '& .MuiToggleButton-root': {
                px: 2,
                py: 0.5,
                borderRadius: 2,
                textTransform: 'none',
                fontWeight: 500,
              },
            }}
          >
            <ToggleButton value="all">全部</ToggleButton>
            <ToggleButton value="dev">{categoryLabels.dev}</ToggleButton>
            <ToggleButton value="daily">{categoryLabels.daily}</ToggleButton>
          </ToggleButtonGroup>
        </Box>
      </Box>

      <Grid container spacing={2} key={animationKey}>
        {filteredTools.map((tool, index) => (
          <Grid size={{ xs: 12, sm: 6, md: 4 }} key={tool.id}>
            <Card
              sx={{
                height: '100%',
                animation: `${expandIn} 0.2s ease-out`,
                animationDelay: `${index * 0.03}s`,
                animationFillMode: 'backwards',
                '@media (hover: hover)': {
                  '&:hover': {
                    transform: 'translateY(-4px)',
                    boxShadow: theme.shadows[4],
                    transition: 'all 0.2s ease-in-out',
                  },
                },
              }}
            >
              <CardActionArea
                onClick={() => navigate(tool.path)}
                sx={{ height: '100%', p: 1 }}
              >
                <CardContent>
                  <Box
                    sx={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 2,
                      mb: 2,
                    }}
                  >
                    <Box
                      sx={{
                        p: 1.5,
                        borderRadius: 2,
                        bgcolor: `${categoryColors[tool.category]}15`,
                        color: categoryColors[tool.category],
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                      }}
                    >
                      {iconMap[tool.icon] || <DataObjectIcon fontSize="large" />}
                    </Box>
                    <Box>
                      <Typography variant="h6" component="h2" sx={{ fontWeight: 600 }}>
                        {tool.name}
                      </Typography>
                      <Chip
                        label={categoryLabels[tool.category]}
                        size="small"
                        sx={{
                          mt: 0.5,
                          height: 20,
                          fontSize: '0.7rem',
                          bgcolor: `${categoryColors[tool.category]}20`,
                          color: categoryColors[tool.category],
                        }}
                      />
                    </Box>
                  </Box>
                  <Typography variant="body2" color="text.secondary">
                    {tool.description}
                  </Typography>
                </CardContent>
              </CardActionArea>
            </Card>
          </Grid>
        ))}
      </Grid>

      {filteredTools.length === 0 && (
        <Box sx={{ textAlign: 'center', py: 8 }}>
          <Typography variant="h6" color="text.secondary">
            找不到符合的工具
          </Typography>
          <Typography variant="body2" color="text.secondary">
            試試其他關鍵字
          </Typography>
        </Box>
      )}
    </Box>
  );
}
