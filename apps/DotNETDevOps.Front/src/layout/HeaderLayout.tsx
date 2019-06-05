
        
import * as tsx from "vue-tsx-support";
import { Component, Prop, Watch } from 'vue-property-decorator';

import "@/assets/less/components/nav.less";

export interface HeaderLayoutOptions {
    backgroundColor: string;
}

@Component
export default class HeaderLayout extends tsx.Component<HeaderLayoutOptions>{

    @Prop({ default:"rgb(248, 127, 46)"})
    backgroundColor!: string;

    render() {
        return (
            <div data-animation="default" data-duration="400" class="navbar w-nav no-print" style={ { backgroundColor: this.backgroundColor, 'will-change':'background' } }>
                <a href="/" class="brand w-nav-brand w--current">
                    DotNETDevOps
                </a>
                <nav role="navigation" class="nav-menu w-nav-menu">
                    {this.$slots.links}
                                  
                </nav>
                <div class="w-nav-button">
                    <div class="w-icon-nav-menu"></div>
                </div>
                <div class="w-nav-overlay" data-wf-ignore=""></div>
            </div>
        );
    }
}
