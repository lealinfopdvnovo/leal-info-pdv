package br.com.lealinfo.vidraceiro;

import android.animation.ValueAnimator;
import android.app.Activity;
import android.content.Intent;
import android.content.pm.ApplicationInfo;
import android.content.res.ColorStateList;
import android.graphics.Color;
import android.os.Bundle;
import android.util.Log;
import android.view.Gravity;
import android.view.View;
import android.view.animation.LinearInterpolator;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.ProgressBar;

/** One shared, visible five-second timeline; no network or project state involved. */
public final class SplashActivity extends Activity {
    private ImageView artwork;
    private ProgressBar progress;
    private ValueAnimator timeline;
    private long elapsed;
    private int lastBucket = -1;
    private boolean leaving;
    private boolean active;

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        if (state != null) elapsed = Math.min(5000, state.getLong("elapsed", 0));
        getWindow().setStatusBarColor(Color.BLACK);
        getWindow().setNavigationBarColor(Color.BLACK);
        FrameLayout frame = new FrameLayout(this);
        frame.setBackgroundColor(Color.BLACK);
        artwork = new ImageView(this);
        artwork.setImageResource(R.drawable.angra_abertura);
        artwork.setScaleType(ImageView.ScaleType.FIT_CENTER);
        artwork.setContentDescription("ANGRA VIDROS");
        artwork.setAlpha(0f);
        frame.addView(artwork, new FrameLayout.LayoutParams(-1, -1));
        progress = new ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal);
        progress.setIndeterminate(false);
        progress.setMax(10000);
        progress.setProgress(0);
        progress.setProgressTintList(ColorStateList.valueOf(Color.rgb(0, 211, 240)));
        progress.setProgressBackgroundTintList(ColorStateList.valueOf(Color.rgb(25, 45, 55)));
        int margin = Math.round(24 * getResources().getDisplayMetrics().density);
        FrameLayout.LayoutParams bar = new FrameLayout.LayoutParams(1, 1, Gravity.TOP | Gravity.LEFT);
        frame.addView(progress, bar);
        // Follow the fitted artwork, not a fixed screen offset. The gap between
        // the slogan and the lower callout is at 67.5% of the original artwork.
        frame.addOnLayoutChangeListener((v, l, t, r, b, ol, ot, or, ob) -> {
            int width = r - l, height = b - t;
            android.graphics.drawable.Drawable image = artwork.getDrawable();
            float scale = Math.min(width / (float) image.getIntrinsicWidth(), height / (float) image.getIntrinsicHeight());
            float imageHeight = image.getIntrinsicHeight() * scale;
            FrameLayout.LayoutParams placement = (FrameLayout.LayoutParams) progress.getLayoutParams();
            placement.width = Math.max(1, width - margin * 2);
            placement.height = Math.max(4, margin / 5);
            placement.leftMargin = margin;
            placement.topMargin = Math.round((height - imageHeight) / 2 + imageHeight * .675f - placement.height / 2f);
            progress.setLayoutParams(placement);
        });
        setContentView(frame);
        apply(elapsed);
    }

    @Override protected void onResume() {
        super.onResume();
        active = true;
        // Post to the rendered view: begin with a genuinely dark frame.
        artwork.postOnAnimation(() -> {
            if (!active || leaving || timeline != null) return;
            timeline = ValueAnimator.ofFloat(elapsed, 5000f);
            timeline.setInterpolator(new LinearInterpolator());
            timeline.setDuration(Math.max(1, 5000 - elapsed));
            timeline.addUpdateListener(animation -> {
                elapsed = Math.round((Float) animation.getAnimatedValue());
                apply(elapsed);
                if (elapsed >= 5000 && !leaving) {
                    leaving = true;
                    startActivity(new Intent(this, MainActivity.class));
                    overridePendingTransition(R.anim.abertura_entrada, R.anim.abertura_saida);
                    finish();
                }
            });
            timeline.start();
        });
    }

    private void apply(long milliseconds) {
        float fade = Math.min(1f, milliseconds / 1500f);
        artwork.setAlpha((float) ((1 - Math.cos(Math.PI * fade)) / 2));
        progress.setProgress((int) (milliseconds * 2));
        int bucket = (int) milliseconds / 250;
        if (bucket != lastBucket && (getApplicationInfo().flags & ApplicationInfo.FLAG_DEBUGGABLE) != 0) {
            lastBucket = bucket;
            Log.i("AngraSplash", "FRAME ms=" + milliseconds + " alpha=" + artwork.getAlpha()
                    + " progress=" + progress.getProgress());
        }
    }

    @Override protected void onPause() {
        active = false;
        if (timeline != null) { timeline.cancel(); timeline = null; }
        super.onPause();
    }

    @Override protected void onSaveInstanceState(Bundle state) {
        state.putLong("elapsed", elapsed);
        super.onSaveInstanceState(state);
    }
}
