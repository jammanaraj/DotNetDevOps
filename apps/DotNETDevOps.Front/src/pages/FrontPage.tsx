

import * as tsx from "vue-tsx-support";
import { Component, Prop, Watch } from 'vue-property-decorator';

import { VBtn } from "vuetify-tsx"

import "@/assets/less/components/sections.less";
//import "@/assets/less/components/btn.less";


export interface FrontPageOptions {

}



export interface WDynItemOptions {
    number: string;
    title: string;
    video?: string;
    
}
@Component
export class WDynItem extends tsx.Component<WDynItemOptions, {}, {info}> {

    

    @Prop({ default: "01" })
    number!: string;

    @Prop()
    title!: string;

    @Prop()
    video!: string;

    top ="-60%";

    mounted() {
        if (this.video) {
            
            let videoElement = document.querySelector("#myVideo") as HTMLVideoElement;
           
            let e = videoElement.parentElement as HTMLDivElement;
            document.addEventListener("scroll", (event) => {
                let bb = e.getBoundingClientRect();
                try {
                    if (!videoElement.currentTime) {
                        //     videoElement.currentTime = (500 - bb.top) / 100
                        //  videoElement.currentTime = 
                        videoElement.play();
                    }
                } catch (err) {
                    console.log(err);
                }
               
                // console.log(bb.top);
                this.top = `calc(50% - ${bb.top}px)`
               // videoElement.style.top = `calc(-100% + ${Math.abs(bb.top)}px)`
              //  console.log(videoElement.style.top);
                //  console.log(this.top);
                //   console.log(this.$data);
            });
        }
       
        var waypoint = new Waypoint({
            element: document.getElementById(this.uniqId),
            
            handler:  (direction)=> {
                console.log(document.getElementById(this.uniqId));
                console.log(direction);
                 
            }
        })
    }
    render() {
      //  console.log(this.$data.top);
        //   console.log(this.video)
        
        let video: JSX.Element | null = null;
        if (this.video) {
           
            video = (
                <video muted={true} id="myVideo" style={{ top: this.top }} >
                    <source src={this.video} type="video/mp4" />
                </video>
            );
        }
        return (
            <div id={this.uniqId} class={{ 'wrapper': true, 'w-dyn-item': true, 'with-video-background': this.video }}>

                {video}
                
                <div class="column vh50">
                    <div class="column _100vh">
                        <div class="project-info">
                            <div class="number">
                                <h2 class="number zero">{this.number}</h2>
                            </div>
                            <h2 class="project-title">{this.title}</h2>

                            {this.$scopedSlots.info(this)}
                           

                        </div>
                    </div>
                    <div class="column _100vh">
                        <div class="project-description">
                            {this.$slots.default}
                        </div>
                    </div>
                </div>
            </div>
        )
    }
}

@Component
export default class FrontPage extends tsx.Component<FrontPageOptions>{






    render() {
       
        return (
            <div class="section main">
                <div class="w-dyn-list">
                    <div class="w-dyn-items">
                        {
                            this.$slots.default
                        }
                    </div>
                </div>

            </div>
        );
    }
}
