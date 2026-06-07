-- Static NPC dialogue pool: ambient lines for quiet days.
-- date IS NULL means reusable. mood = bullish/bearish/neutral. phase = pre_market/post_market or NULL (any).

-- ══════════════════════════════════════
-- ANALYST
-- ══════════════════════════════════════

-- Neutral
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('analyst', 'ambient', 'Nothing unusual in the numbers today. Sometimes the most important signal is the absence of one.', 'neutral', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('analyst', 'ambient', 'I''ve been looking at the 20-day moving averages. Most tickers are trading right around their mean.', 'neutral', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('analyst', 'ambient', 'Low-volatility days like this are good for reviewing your thesis on each position.', 'neutral', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('analyst', 'ambient', 'Markets are range-bound today. No strong directional bias in the data.', 'neutral', NULL, 'low', 0);

-- Bullish
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('analyst', 'ambient', 'The technicals are looking constructive. Breadth is solid across most sectors.', 'bullish', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('analyst', 'ambient', 'We''re seeing higher lows on most charts. The trend is your friend until it bends.', 'bullish', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('analyst', 'ambient', 'Momentum indicators are positive. This is the kind of environment where staying invested tends to pay off.', 'bullish', NULL, 'low', 0);

-- Bearish
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('analyst', 'ambient', 'I''d keep a close eye on your positions. The macro environment is... uncertain at best.', 'bearish', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('analyst', 'ambient', 'Defensive posture makes sense right now. Cash isn''t exciting, but it doesn''t lose 5% overnight either.', 'bearish', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('analyst', 'ambient', 'The data isn''t great. Revenue estimates are coming down across the board.', 'bearish', NULL, 'low', 0);

-- Pre-market specific
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('analyst', 'ambient', 'Morning. I''ve been going through the numbers since 5 AM. Nothing earth-shattering yet.', 'neutral', 'pre_market', 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('analyst', 'ambient', 'Pre-market is your planning window. Review your positions, set your orders, then let the market come to you.', 'neutral', 'pre_market', 'low', 0);

-- Post-market specific
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('analyst', 'ambient', 'Markets are closed. Good time to review what the closing prices tell us about tomorrow.', 'neutral', 'post_market', 'low', 0);

-- ══════════════════════════════════════
-- BROKER
-- ══════════════════════════════════════

-- Neutral
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('broker', 'ambient', 'Quiet day on the floor. Sometimes the best trade is no trade at all.', 'neutral', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('broker', 'ambient', 'Everyone''s waiting for a catalyst. When the street gets this quiet, something usually shakes loose.', 'neutral', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('broker', 'ambient', 'You know what separates the pros from the amateurs? Patience on days like this.', 'neutral', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('broker', 'ambient', 'If you''re not sure what to do, do nothing. That''s advice most people on Wall Street can''t follow.', 'neutral', NULL, 'low', 0);

-- Bullish
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('broker', 'ambient', 'The mood on the floor is good. Money wants to work right now.', 'bullish', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('broker', 'ambient', 'Clients are calling to buy, not sell. That tells you something about where sentiment is.', 'bullish', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('broker', 'ambient', 'I love days like this. Everyone''s making money, phones are ringing. This is why we do this.', 'bullish', NULL, 'low', 0);

-- Bearish
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('broker', 'ambient', 'Lot of nervous people today. When your clients can''t sleep, that''s usually when the real selling starts.', 'bearish', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('broker', 'ambient', 'I''m telling my clients to raise cash. Better safe than sorry in this kind of market.', 'bearish', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('broker', 'ambient', 'Fear is contagious on Wall Street. Once it starts spreading, it feeds on itself.', 'bearish', NULL, 'low', 0);

-- Pre-market
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('broker', 'ambient', 'Good morning! Got your orders ready? The opening bell waits for nobody.', 'neutral', 'pre_market', 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('broker', 'ambient', 'Smart traders do their homework before the bell. Reactive trading is expensive trading.', 'neutral', 'pre_market', 'low', 0);

-- Post-market
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('broker', 'ambient', 'Another day in the books. How''d you do? Remember, it''s a marathon, not a sprint.', 'neutral', 'post_market', 'low', 0);

-- ══════════════════════════════════════
-- TRADER
-- ══════════════════════════════════════

-- Neutral
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('trader', 'ambient', 'Slow day. I hate slow days. Nothing to do but wait.', 'neutral', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('trader', 'ambient', 'Price doesn''t lie. Charts don''t have opinions. That''s why I trust ''em.', 'neutral', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('trader', 'ambient', 'You holding anything overnight? Bold move. I respect it.', 'neutral', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('trader', 'ambient', 'Been doing this 20 years. Every day still feels different, and somehow exactly the same.', 'neutral', NULL, 'low', 0);

-- Bullish
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('trader', 'ambient', 'Green across the board. Love to see it. Don''t get greedy though.', 'bullish', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('trader', 'ambient', 'Bulls are running. The trick is knowing when to get off the bull.', 'bullish', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('trader', 'ambient', 'Everything''s working right now. That''s usually when you should start getting cautious.', 'bullish', NULL, 'low', 0);

-- Bearish
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('trader', 'ambient', 'Ugly out there. Real ugly. I''m mostly in cash.', 'bearish', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('trader', 'ambient', 'Don''t try to catch a falling knife. Let it hit the floor first.', 'bearish', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('trader', 'ambient', 'Seen this before. People panic, sell everything, then regret it three months later.', 'bearish', NULL, 'low', 0);

-- Pre-market
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('trader', 'ambient', 'Opening bell''s coming. Got a feeling about today. Gut says interesting.', 'neutral', 'pre_market', 'low', 0);

-- Post-market
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('trader', 'ambient', 'Bell rang. Time to see who made money and who''s making excuses.', 'neutral', 'post_market', 'low', 0);

-- ══════════════════════════════════════
-- ANCHOR
-- ══════════════════════════════════════

-- Neutral
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('anchor', 'ambient', 'Markets traded in a narrow range today with no major catalysts driving price action.', 'neutral', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('anchor', 'ambient', 'Trading volume was lighter than average. Investors appear to be in a wait-and-see mode.', 'neutral', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('anchor', 'ambient', 'No significant economic data releases scheduled today. Markets are largely directionless.', 'neutral', NULL, 'low', 0);

-- Bullish
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('anchor', 'ambient', 'Markets continue their upward trajectory as investor confidence remains strong.', 'bullish', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('anchor', 'ambient', 'Broad-based gains across sectors today, with advancing issues outnumbering decliners.', 'bullish', NULL, 'low', 0);

-- Bearish
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('anchor', 'ambient', 'Selling pressure continued today as investors weighed growing economic concerns.', 'bearish', NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('anchor', 'ambient', 'Markets remain under pressure. Analysts are watching key support levels closely.', 'bearish', NULL, 'low', 0);

-- Pre-market
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('anchor', 'ambient', 'Good morning. Here''s what we''re watching as markets prepare to open.', 'neutral', 'pre_market', 'low', 0);

-- Post-market
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('anchor', 'ambient', 'That''s a wrap on today''s trading session. Here''s a look at how things settled.', 'neutral', 'post_market', 'low', 0);

-- ══════════════════════════════════════
-- TRADING WISDOM (any NPC, educational)
-- ══════════════════════════════════════

INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('broker', 'wisdom', 'Remember — diversification isn''t just about owning more stocks. It''s about owning different kinds of risk.', NULL, NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('analyst', 'wisdom', 'A stock price is just what someone is willing to pay right now. It''s not a verdict on the company''s worth.', NULL, NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('trader', 'wisdom', 'Plan the trade, trade the plan. Know your exit before you enter.', NULL, NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('broker', 'wisdom', 'The best investors I know all have one thing in common — they keep cash for opportunities.', NULL, NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('analyst', 'wisdom', 'Past performance doesn''t predict future results. But it does tell you about the risks you''re taking on.', NULL, NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('trader', 'wisdom', 'Losses are tuition. The question is whether you''re learning from them.', NULL, NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('anchor', 'wisdom', 'Historically, market downturns have always been followed by recoveries. Timing them, however, has proven nearly impossible.', NULL, NULL, 'low', 0);
INSERT INTO static_npc_dialogue (npc_type, category, text, mood, phase, priority, line_order) VALUES
('broker', 'wisdom', 'You don''t need to trade every day. Sometimes the most profitable thing you can do is nothing.', NULL, NULL, 'low', 0);
